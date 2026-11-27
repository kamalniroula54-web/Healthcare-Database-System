using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class UserAccountService(
	LocalDatabase database,
	IPasswordHasher<LocalAdminUser> passwordHasher) : IUserAccountService
{
	public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT EXISTS(SELECT 1 FROM UserAccounts WHERE Role = $Role)";
		command.Parameters.AddWithValue("$Role", UserRoles.Admin);
		return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
	}

	public async Task<AccountAuthenticationState?> GetAuthenticationStateAsync(
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT Role, MustChangePassword, ProfileComplete, HospitalId, IsHospitalAdmin, CanApprovePatientAccess " +
			"FROM UserAccounts WHERE Username = $Username";
		command.Parameters.AddWithValue("$Username", username.Trim());
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new AccountAuthenticationState(
				reader.GetString(0),
				reader.GetInt32(1) != 0,
				reader.GetInt32(2) != 0,
				reader.GetInt32(3),
				reader.GetInt32(4) != 0,
				reader.GetInt32(5) != 0)
			: null;
	}

	public async Task<bool> CreateInitialAdminAsync(
		string username,
		string password,
		EmployeeProfile profile,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();
		await using SqliteCommand existsCommand = connection.CreateCommand();
		existsCommand.Transaction = transaction;
		existsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM UserAccounts WHERE Role = $Role)";
		existsCommand.Parameters.AddWithValue("$Role", UserRoles.Admin);
		if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) != 0)
			return false;

		string normalizedUsername = username.Trim();
		string passwordHash = passwordHasher.HashPassword(new LocalAdminUser(normalizedUsername, string.Empty), password);
		await using SqliteCommand insertCommand = connection.CreateCommand();
		insertCommand.Transaction = transaction;
		insertCommand.CommandText =
			"""
			INSERT INTO UserAccounts (
				Username, PasswordHash, Role, MustChangePassword, FirstName, LastName,
				DateOfBirth, Email, Phone, EmergencyContactName, EmergencyContactPhone,
				EmergencyContactRelationship, ClinicalRole, ProfileComplete)
			VALUES (
				$Username, $PasswordHash, $Role, 0, $FirstName, $LastName,
				$DateOfBirth, $Email, $Phone, $EmergencyContactName, $EmergencyContactPhone,
				$EmergencyContactRelationship, $ClinicalRole, 1)
			""";
		insertCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		insertCommand.Parameters.AddWithValue("$PasswordHash", passwordHash);
		insertCommand.Parameters.AddWithValue("$Role", UserRoles.Admin);
		AddProfileParameters(insertCommand, profile);
		await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<UserAccount?> AuthenticateAsync(
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT Username, PasswordHash, Role, MustChangePassword, ProfileComplete, HospitalId, " +
			"IsHospitalAdmin, CanApprovePatientAccess " +
			"FROM UserAccounts WHERE Username=$Username";
		command.Parameters.AddWithValue("$Username", username.Trim());
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return null;

		LocalAdminUser user = new(reader.GetString(0), reader.GetString(1));
		PasswordVerificationResult result =
			passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
		return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
			? new UserAccount(
				user.Username,
				user.PasswordHash,
				reader.GetString(2),
				reader.GetInt32(3) != 0,
				reader.GetInt32(4) != 0,
				reader.GetInt32(5),
				reader.GetInt32(6) != 0,
				reader.GetInt32(7) != 0)
			: null;
	}

	public async Task<string?> CreateEmployeeAsync(
		string username,
		int hospitalId,
		CancellationToken cancellationToken) =>
		await CreateAccountAsync(username, UserRoles.Employee, hospitalId, cancellationToken);

	public async Task<string?> CreateAdministratorAsync(string username, CancellationToken cancellationToken)
		=> await CreateAccountAsync(username, UserRoles.Admin, 1, cancellationToken);

	public async Task<string?> CreateHospitalAdministratorAsync(
		string username,
		int hospitalId,
		bool canApprovePatientAccess,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		string temporaryPassword = CreateTemporaryPassword();
		string passwordHash = HashPassword(normalizedUsername, temporaryPassword);

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			INSERT INTO UserAccounts (
				Username, PasswordHash, Role, MustChangePassword, HospitalId,
				IsHospitalAdmin, CanApprovePatientAccess)
			SELECT $Username, $PasswordHash, $Role, 1, $HospitalId, 1, $CanApprovePatientAccess
			WHERE EXISTS (SELECT 1 FROM Hospitals WHERE Id = $HospitalId AND IsActive = 1)
			ON CONFLICT(Username) DO NOTHING;
			""";
		command.Parameters.AddWithValue("$Username", normalizedUsername);
		command.Parameters.AddWithValue("$PasswordHash", passwordHash);
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		command.Parameters.AddWithValue("$HospitalId", hospitalId);
		command.Parameters.AddWithValue("$CanApprovePatientAccess", canApprovePatientAccess);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? temporaryPassword : null;
	}

	private async Task<string?> CreateAccountAsync(
		string username,
		string role,
		int hospitalId,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		string temporaryPassword = CreateTemporaryPassword();
		string passwordHash = HashPassword(normalizedUsername, temporaryPassword);

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			INSERT INTO UserAccounts (Username, PasswordHash, Role, MustChangePassword, HospitalId)
			VALUES ($Username, $PasswordHash, $Role, 1, $HospitalId)
			ON CONFLICT(Username) DO NOTHING;
			""";
		command.Parameters.AddWithValue("$Username", normalizedUsername);
		command.Parameters.AddWithValue("$PasswordHash", passwordHash);
		command.Parameters.AddWithValue("$Role", role);
		command.Parameters.AddWithValue("$HospitalId", hospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? temporaryPassword : null;
	}

	public async Task<string?> ResetEmployeePasswordAsync(
		string username,
		int? hospitalId,
		bool includeHospitalAdmins,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		string temporaryPassword = CreateTemporaryPassword();
		string passwordHash = HashPassword(normalizedUsername, temporaryPassword);

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET PasswordHash = $PasswordHash, MustChangePassword = 1
			WHERE Username = $Username AND Role = $Role
				AND IsHospitalAdmin = $IsHospitalAdmin
				AND ($HospitalId IS NULL OR HospitalId = $HospitalId)
				AND NOT EXISTS (
					SELECT 1 FROM PasswordResetRequests
					WHERE Username = $Username AND ResolvedAtUtc IS NULL)
			""";
		command.Parameters.AddWithValue("$PasswordHash", passwordHash);
		command.Parameters.AddWithValue("$Username", normalizedUsername);
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		command.Parameters.AddWithValue("$IsHospitalAdmin", includeHospitalAdmins);
		command.Parameters.AddWithValue("$HospitalId", (object?)hospitalId ?? DBNull.Value);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? temporaryPassword : null;
	}

	public async Task<string?> ResetAdministratorPasswordAsync(
		string username,
		string requestingAdminUsername,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		if (string.Equals(normalizedUsername, requestingAdminUsername, StringComparison.OrdinalIgnoreCase))
			return null;

		string temporaryPassword = CreateTemporaryPassword();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET PasswordHash = $PasswordHash, MustChangePassword = 1
			WHERE Username = $Username AND Role = $Role
				AND NOT EXISTS (
					SELECT 1 FROM PasswordResetRequests
					WHERE Username = $Username AND ResolvedAtUtc IS NULL)
			""";
		command.Parameters.AddWithValue("$PasswordHash", HashPassword(normalizedUsername, temporaryPassword));
		command.Parameters.AddWithValue("$Username", normalizedUsername);
		command.Parameters.AddWithValue("$Role", UserRoles.Admin);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1 ? temporaryPassword : null;
	}

	public Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
		int? hospitalId,
		CancellationToken cancellationToken) =>
		GetAccountsAsync(UserRoles.Employee, hospitalId, false, cancellationToken);

	public async Task<EmployeeAccountSummary?> GetEmployeeAsync(
		string username,
		int? hospitalId,
		CancellationToken cancellationToken) =>
		(await GetEmployeesAsync(hospitalId, cancellationToken))
			.FirstOrDefault(account =>
				string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase));

	public Task<IReadOnlyList<EmployeeAccountSummary>> GetAdministratorsAsync(CancellationToken cancellationToken) =>
		GetAccountsAsync(UserRoles.Admin, null, false, cancellationToken);

	public Task<IReadOnlyList<EmployeeAccountSummary>> GetHospitalAdministratorsAsync(
		CancellationToken cancellationToken) =>
		GetAccountsAsync(UserRoles.Employee, null, true, cancellationToken);

	public async Task<bool> UpdateHospitalAdministratorAsync(
		string username,
		int hospitalId,
		bool canApprovePatientAccess,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET HospitalId = $HospitalId, CanApprovePatientAccess = $CanApprovePatientAccess
			WHERE Username = $Username AND Role = $Role AND IsHospitalAdmin = 1
				AND EXISTS (SELECT 1 FROM Hospitals WHERE Id = $HospitalId AND IsActive = 1)
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		command.Parameters.AddWithValue("$HospitalId", hospitalId);
		command.Parameters.AddWithValue("$CanApprovePatientAccess", canApprovePatientAccess);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<bool> RemoveHospitalAdministratorAsync(
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET IsHospitalAdmin = 0, CanApprovePatientAccess = 0
			WHERE Username = $Username AND Role = $Role AND IsHospitalAdmin = 1
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private async Task<IReadOnlyList<EmployeeAccountSummary>> GetAccountsAsync(
		string role,
		int? hospitalId,
		bool isHospitalAdmin,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT account.Username, account.FirstName, account.LastName, account.DateOfBirth,
				account.Email, account.Phone,
				EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship,
				account.ClinicalRole,
				account.ProfileComplete, account.MustChangePassword, account.HospitalId, hospital.Name,
				account.IsHospitalAdmin, account.CanApprovePatientAccess
			FROM UserAccounts account
			JOIN Hospitals hospital ON hospital.Id = account.HospitalId
			WHERE account.Role = $Role
				AND account.IsHospitalAdmin = $IsHospitalAdmin
				AND ($HospitalId IS NULL OR account.HospitalId = $HospitalId)
			ORDER BY account.LastName, account.FirstName, account.Username
			""";
		command.Parameters.AddWithValue("$Role", role);
		command.Parameters.AddWithValue("$IsHospitalAdmin", isHospitalAdmin);
		command.Parameters.AddWithValue("$HospitalId", (object?)hospitalId ?? DBNull.Value);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<EmployeeAccountSummary> employees = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			employees.Add(new EmployeeAccountSummary(
				reader.GetString(0),
				ReadProfile(reader, 1),
				reader.GetInt32(10) != 0,
				reader.GetInt32(11) != 0,
				reader.GetInt32(12),
				reader.GetString(13),
				reader.GetInt32(14) != 0,
				reader.GetInt32(15) != 0));
		}

		return employees;
	}

	public async Task RequestPhonePasswordResetAsync(
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			INSERT OR IGNORE INTO PasswordResetRequests (
				Username, AccountRole, ContactMethod, RequestedAtUtc, DeliveryStatus)
			SELECT Username, Role, 'Phone', $RequestedAtUtc, 'ManualPending'
			FROM UserAccounts
			WHERE Username = $Username AND Role IN ($AdminRole, $EmployeeRole)
				AND NOT EXISTS (
					SELECT 1 FROM PasswordResetRequests
					WHERE Username = $Username AND ResolvedAtUtc IS NULL)
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		command.Parameters.AddWithValue("$RequestedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
		command.Parameters.AddWithValue("$AdminRole", UserRoles.Admin);
		command.Parameters.AddWithValue("$EmployeeRole", UserRoles.Employee);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<EmailPasswordReset?> PrepareEmailPasswordResetAsync(
		string username,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		DateTime now = DateTime.UtcNow;
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand attemptCommand = connection.CreateCommand();
		attemptCommand.Transaction = transaction;
		attemptCommand.CommandText =
			"""
			UPDATE UserAccounts
			SET PasswordResetEmailAttemptedAtUtc = $AttemptedAtUtc
			WHERE Username = $Username AND Role IN ($AdminRole, $EmployeeRole)
				AND (PasswordResetEmailAttemptedAtUtc IS NULL OR PasswordResetEmailAttemptedAtUtc <= $CooldownCutoffUtc)
				AND NOT EXISTS (
					SELECT 1 FROM PasswordResetRequests
					WHERE Username = $Username AND ResolvedAtUtc IS NULL)
			""";
		attemptCommand.Parameters.AddWithValue("$AttemptedAtUtc", now.ToString("O", CultureInfo.InvariantCulture));
		attemptCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		attemptCommand.Parameters.AddWithValue(
			"$CooldownCutoffUtc",
			now.AddMinutes(-10).ToString("O", CultureInfo.InvariantCulture));
		attemptCommand.Parameters.AddWithValue("$AdminRole", UserRoles.Admin);
		attemptCommand.Parameters.AddWithValue("$EmployeeRole", UserRoles.Employee);
		if (await attemptCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return null;

		await using SqliteCommand accountCommand = connection.CreateCommand();
		accountCommand.Transaction = transaction;
		accountCommand.CommandText =
			"""
			SELECT Username, Role, FirstName, Email, Phone
			FROM UserAccounts
			WHERE Username = $Username AND Role IN ($AdminRole, $EmployeeRole)
			""";
		accountCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		accountCommand.Parameters.AddWithValue("$AdminRole", UserRoles.Admin);
		accountCommand.Parameters.AddWithValue("$EmployeeRole", UserRoles.Employee);
		await using SqliteDataReader accountReader = await accountCommand.ExecuteReaderAsync(cancellationToken);
		if (!await accountReader.ReadAsync(cancellationToken))
			return null;

		string accountUsername = accountReader.GetString(0);
		string accountRole = accountReader.GetString(1);
		string firstName = accountReader.GetString(2);
		string email = accountReader.GetString(3);
		await accountReader.DisposeAsync();

		string temporaryPassword = CreateTemporaryPassword();
		string? deliveryError = string.IsNullOrWhiteSpace(email) ? "EmailAddressMissing" : null;
		string deliveryStatus = deliveryError is null ? "Sending" : "Failed";
		await using SqliteCommand insertRequestCommand = connection.CreateCommand();
		insertRequestCommand.Transaction = transaction;
		insertRequestCommand.CommandText =
			"""
			INSERT INTO PasswordResetRequests (
				Username, AccountRole, ContactMethod, RequestedAtUtc, DeliveryStatus, DeliveryError)
			VALUES ($Username, $AccountRole, 'Email', $RequestedAtUtc, $DeliveryStatus, $DeliveryError)
			""";
		insertRequestCommand.Parameters.AddWithValue("$Username", accountUsername);
		insertRequestCommand.Parameters.AddWithValue("$AccountRole", accountRole);
		insertRequestCommand.Parameters.AddWithValue("$RequestedAtUtc", now.ToString("O", CultureInfo.InvariantCulture));
		insertRequestCommand.Parameters.AddWithValue("$DeliveryStatus", deliveryStatus);
		insertRequestCommand.Parameters.AddWithValue("$DeliveryError", (object?)deliveryError ?? DBNull.Value);
		await insertRequestCommand.ExecuteNonQueryAsync(cancellationToken);
		await using SqliteCommand requestIdCommand = connection.CreateCommand();
		requestIdCommand.Transaction = transaction;
		requestIdCommand.CommandText = "SELECT last_insert_rowid()";
		int requestId = Convert.ToInt32(await requestIdCommand.ExecuteScalarAsync(cancellationToken));
		if (deliveryError is not null)
		{
			await transaction.CommitAsync(cancellationToken);
			return null;
		}

		await transaction.CommitAsync(cancellationToken);
		return new EmailPasswordReset(requestId, accountUsername, firstName, email, temporaryPassword);
	}

	public async Task<bool> CompleteEmailPasswordResetAsync(
		int requestId,
		string temporaryPassword,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();
		await using SqliteCommand requestCommand = connection.CreateCommand();
		requestCommand.Transaction = transaction;
		requestCommand.CommandText =
			"""
			SELECT Username, AccountRole
			FROM PasswordResetRequests
			WHERE Id = $Id AND ResolvedAtUtc IS NULL AND DeliveryStatus = 'Sending'
			""";
		requestCommand.Parameters.AddWithValue("$Id", requestId);
		await using SqliteDataReader reader = await requestCommand.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return false;

		string username = reader.GetString(0);
		string role = reader.GetString(1);
		await reader.DisposeAsync();

		await using SqliteCommand accountCommand = connection.CreateCommand();
		accountCommand.Transaction = transaction;
		accountCommand.CommandText =
			"""
			UPDATE UserAccounts
			SET PasswordHash = $PasswordHash, MustChangePassword = 1
			WHERE Username = $Username AND Role = $Role
			""";
		accountCommand.Parameters.AddWithValue("$PasswordHash", HashPassword(username, temporaryPassword));
		accountCommand.Parameters.AddWithValue("$Username", username);
		accountCommand.Parameters.AddWithValue("$Role", role);
		if (await accountCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return false;

		await using SqliteCommand resolveCommand = connection.CreateCommand();
		resolveCommand.Transaction = transaction;
		resolveCommand.CommandText =
			"""
			UPDATE PasswordResetRequests
			SET DeliveryStatus = 'Sent', DeliveryError = NULL,
				ResolvedAtUtc = $ResolvedAtUtc, ResolvedBy = 'AutomaticEmail'
			WHERE Id = $Id AND ResolvedAtUtc IS NULL AND DeliveryStatus = 'Sending'
			""";
		resolveCommand.Parameters.AddWithValue("$ResolvedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
		resolveCommand.Parameters.AddWithValue("$Id", requestId);
		if (await resolveCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return false;

		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task MarkPasswordResetEmailFailedAsync(
		int requestId,
		string errorCode,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE PasswordResetRequests
			SET DeliveryStatus = 'Failed', DeliveryError = $DeliveryError
			WHERE Id = $Id AND ResolvedAtUtc IS NULL AND DeliveryStatus = 'Sending'
			""";
		command.Parameters.AddWithValue("$DeliveryError", errorCode);
		command.Parameters.AddWithValue("$Id", requestId);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<PasswordResetRequest>> GetPendingPasswordResetRequestsAsync(
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand staleDeliveryCommand = connection.CreateCommand();
		staleDeliveryCommand.CommandText =
			"""
			UPDATE PasswordResetRequests
			SET DeliveryStatus = 'Failed', DeliveryError = 'DeliveryTimedOut'
			WHERE ResolvedAtUtc IS NULL AND DeliveryStatus = 'Sending'
				AND RequestedAtUtc <= $CutoffUtc
			""";
		staleDeliveryCommand.Parameters.AddWithValue(
			"$CutoffUtc",
			DateTime.UtcNow.AddMinutes(-5).ToString("O", CultureInfo.InvariantCulture));
		await staleDeliveryCommand.ExecuteNonQueryAsync(cancellationToken);

		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT request.Id, request.Username, request.AccountRole, request.ContactMethod,
				request.RequestedAtUtc, account.Email, account.Phone,
				request.DeliveryStatus, request.DeliveryError
			FROM PasswordResetRequests request
			JOIN UserAccounts account ON account.Username = request.Username
			WHERE request.ResolvedAtUtc IS NULL AND account.Role = request.AccountRole
			ORDER BY request.RequestedAtUtc
			""";
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PasswordResetRequest> requests = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			requests.Add(new PasswordResetRequest(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				reader.GetString(5),
				reader.GetString(6),
				reader.GetString(7),
				reader.IsDBNull(8) ? null : reader.GetString(8)));
		}

		return requests;
	}

	public async Task<EmployeePasswordReset?> IssueRequestedPasswordResetAsync(
		int requestId,
		string adminUsername,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand requestCommand = connection.CreateCommand();
		requestCommand.Transaction = transaction;
		requestCommand.CommandText =
			"""
			SELECT request.Username, request.AccountRole, request.ContactMethod,
				account.FirstName, account.Email, account.Phone, request.DeliveryError
			FROM PasswordResetRequests request
			JOIN UserAccounts account ON account.Username = request.Username
			WHERE request.Id = $Id AND request.ResolvedAtUtc IS NULL
				AND request.DeliveryStatus IN ('ManualPending', 'Failed')
				AND account.Role = request.AccountRole
				AND (request.AccountRole != $AdminRole OR request.Username != $AdminUsername)
			""";
		requestCommand.Parameters.AddWithValue("$Id", requestId);
		requestCommand.Parameters.AddWithValue("$AdminRole", UserRoles.Admin);
		requestCommand.Parameters.AddWithValue("$AdminUsername", adminUsername);
		await using SqliteDataReader reader = await requestCommand.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return null;

		string username = reader.GetString(0);
		string role = reader.GetString(1);
		string contactMethod = reader.GetString(2);
		string firstName = reader.GetString(3);
		string email = reader.GetString(4);
		string phone = reader.GetString(5);
		string? deliveryError = reader.IsDBNull(6) ? null : reader.GetString(6);
		await reader.DisposeAsync();

		string temporaryPassword = CreateTemporaryPassword();
		await using SqliteCommand updateAccountCommand = connection.CreateCommand();
		updateAccountCommand.Transaction = transaction;
		updateAccountCommand.CommandText =
			"""
			UPDATE UserAccounts
			SET PasswordHash = $PasswordHash, MustChangePassword = 1
			WHERE Username = $Username AND Role = $Role
			""";
		updateAccountCommand.Parameters.AddWithValue("$PasswordHash", HashPassword(username, temporaryPassword));
		updateAccountCommand.Parameters.AddWithValue("$Username", username);
		updateAccountCommand.Parameters.AddWithValue("$Role", role);
		if (await updateAccountCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return null;

		await using SqliteCommand resolveRequestCommand = connection.CreateCommand();
		resolveRequestCommand.Transaction = transaction;
		resolveRequestCommand.CommandText =
			"""
			UPDATE PasswordResetRequests
			SET DeliveryStatus = 'Sent', ResolvedAtUtc = $ResolvedAtUtc, ResolvedBy = $ResolvedBy
			WHERE Id = $Id AND ResolvedAtUtc IS NULL
			""";
		resolveRequestCommand.Parameters.AddWithValue("$ResolvedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
		resolveRequestCommand.Parameters.AddWithValue("$ResolvedBy", adminUsername);
		resolveRequestCommand.Parameters.AddWithValue("$Id", requestId);
		if (await resolveRequestCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return null;

		await transaction.CommitAsync(cancellationToken);
		return new EmployeePasswordReset(username, firstName, email, phone, contactMethod, temporaryPassword, deliveryError);
	}

	public async Task<EmployeeProfile?> GetProfileAsync(string username, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT FirstName, LastName, DateOfBirth, Email, Phone,
				EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship, ClinicalRole
			FROM UserAccounts
			WHERE Username = $Username
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadProfile(reader, 0) : null;
	}

	public async Task<EmployeeProfile?> GetEmployeeProfileAsync(string username, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT FirstName, LastName, DateOfBirth, Email, Phone,
				EmergencyContactName, EmergencyContactPhone, EmergencyContactRelationship, ClinicalRole
			FROM UserAccounts
			WHERE Username = $Username AND Role = $Role
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadProfile(reader, 0) : null;
	}

	public Task<bool> UpdateProfileAsync(
		string username,
		EmployeeProfile profile,
		CancellationToken cancellationToken) =>
		UpdateProfileAsync(username, profile, null, cancellationToken);

	public Task<bool> UpdateEmployeeProfileAsync(
		string username,
		EmployeeProfile profile,
		int hospitalId,
		int? expectedHospitalId,
		CancellationToken cancellationToken) =>
		UpdateEmployeeProfileAndHospitalAsync(
			username, profile, hospitalId, expectedHospitalId, cancellationToken);

	public async Task<bool> CompleteAccountSetupAsync(
		string username,
		EmployeeProfile profile,
		string? newPassword,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		if (newPassword is null)
		{
			command.CommandText =
				"""
				UPDATE UserAccounts
				SET FirstName = $FirstName, LastName = $LastName, DateOfBirth = $DateOfBirth,
					Email = $Email, Phone = $Phone, EmergencyContactName = $EmergencyContactName,
					EmergencyContactPhone = $EmergencyContactPhone,
					EmergencyContactRelationship = $EmergencyContactRelationship,
					ClinicalRole = $ClinicalRole, ProfileComplete = 1
				WHERE Username = $Username AND ProfileComplete = 0 AND MustChangePassword = 0
				""";
		}
		else
		{
			command.CommandText =
				"""
				UPDATE UserAccounts
				SET PasswordHash = $PasswordHash, MustChangePassword = 0,
					FirstName = $FirstName, LastName = $LastName, DateOfBirth = $DateOfBirth,
					Email = $Email, Phone = $Phone, EmergencyContactName = $EmergencyContactName,
					EmergencyContactPhone = $EmergencyContactPhone,
					EmergencyContactRelationship = $EmergencyContactRelationship,
					ClinicalRole = $ClinicalRole, ProfileComplete = 1
				WHERE Username = $Username AND MustChangePassword = 1
				""";
			command.Parameters.AddWithValue("$PasswordHash", HashPassword(normalizedUsername, newPassword));
		}

		command.Parameters.AddWithValue("$Username", normalizedUsername);
		AddProfileParameters(command, profile);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private async Task<bool> UpdateProfileAsync(
		string username,
		EmployeeProfile profile,
		string? requiredRole,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET FirstName = $FirstName, LastName = $LastName, DateOfBirth = $DateOfBirth,
				Email = $Email, Phone = $Phone, EmergencyContactName = $EmergencyContactName,
				EmergencyContactPhone = $EmergencyContactPhone,
				EmergencyContactRelationship = $EmergencyContactRelationship,
				ClinicalRole = $ClinicalRole, ProfileComplete = 1
			WHERE Username = $Username
			""" + (requiredRole is null ? string.Empty : " AND Role = $Role");
		command.Parameters.AddWithValue("$Username", username.Trim());
		AddProfileParameters(command, profile);
		if (requiredRole is not null)
			command.Parameters.AddWithValue("$Role", requiredRole);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private async Task<bool> UpdateEmployeeProfileAndHospitalAsync(
		string username,
		EmployeeProfile profile,
		int hospitalId,
		int? expectedHospitalId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE UserAccounts
			SET FirstName = $FirstName, LastName = $LastName, DateOfBirth = $DateOfBirth,
				Email = $Email, Phone = $Phone, EmergencyContactName = $EmergencyContactName,
				EmergencyContactPhone = $EmergencyContactPhone,
				EmergencyContactRelationship = $EmergencyContactRelationship,
				ClinicalRole = $ClinicalRole, ProfileComplete = 1, HospitalId = $HospitalId
			WHERE Username = $Username AND Role = $Role
				AND IsHospitalAdmin = 0
				AND ($ExpectedHospitalId IS NULL OR HospitalId = $ExpectedHospitalId)
				AND EXISTS (SELECT 1 FROM Hospitals WHERE Id = $HospitalId AND IsActive = 1)
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		command.Parameters.AddWithValue("$Role", UserRoles.Employee);
		command.Parameters.AddWithValue("$HospitalId", hospitalId);
		command.Parameters.AddWithValue("$ExpectedHospitalId", (object?)expectedHospitalId ?? DBNull.Value);
		AddProfileParameters(command, profile);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private static void AddProfileParameters(SqliteCommand command, EmployeeProfile profile)
	{
		command.Parameters.AddWithValue("$FirstName", profile.FirstName.Trim());
		command.Parameters.AddWithValue("$LastName", profile.LastName.Trim());
		command.Parameters.Add("$DateOfBirth", SqliteType.Text).Value =
			profile.DateOfBirth.HasValue ? FormatDate(profile.DateOfBirth.Value) : DBNull.Value;
		command.Parameters.AddWithValue("$Email", profile.Email.Trim());
		command.Parameters.AddWithValue("$Phone", profile.Phone.Trim());
		command.Parameters.AddWithValue("$EmergencyContactName", profile.EmergencyContactName.Trim());
		command.Parameters.AddWithValue("$EmergencyContactPhone", profile.EmergencyContactPhone.Trim());
		command.Parameters.AddWithValue("$EmergencyContactRelationship", profile.EmergencyContactRelationship.Trim());
		command.Parameters.AddWithValue("$ClinicalRole", profile.ClinicalRole.Trim());
	}

	private static EmployeeProfile ReadProfile(SqliteDataReader reader, int startIndex) =>
		new(
			reader.GetString(startIndex),
			reader.GetString(startIndex + 1),
			reader.IsDBNull(startIndex + 2) ? null : DateTime.Parse(reader.GetString(startIndex + 2), CultureInfo.InvariantCulture),
			reader.GetString(startIndex + 3),
			reader.GetString(startIndex + 4),
			reader.GetString(startIndex + 5),
			reader.GetString(startIndex + 6),
			reader.GetString(startIndex + 7),
			reader.GetString(startIndex + 8));

	private string HashPassword(string username, string password) =>
		passwordHasher.HashPassword(new LocalAdminUser(username, string.Empty), password);

	private static string CreateTemporaryPassword() =>
		Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
			.TrimEnd('=').Replace('+', '-').Replace('/', '_');

	private static string FormatDate(DateTime date) =>
		date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
