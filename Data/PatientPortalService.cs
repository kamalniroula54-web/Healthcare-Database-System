using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class PatientPortalService(
	LocalDatabase database,
	CurrentHospitalContext currentHospital,
	IPasswordHasher<PatientPortalUser> passwordHasher) : IPatientPortalService
{
	public async Task<PatientPortalUser?> AuthenticateAsync(
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT account.Username, account.PasswordHash
			FROM PatientPortalAccounts account
			WHERE account.Username = $Username
				AND EXISTS (
					SELECT 1 FROM PatientPortalIdentityLinks identityLink
					JOIN Patients patient ON patient.SharedPatientId = identityLink.SharedPatientId
					WHERE identityLink.Username = account.Username)
			""";
		command.Parameters.AddWithValue("$Username", normalizedUsername);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return null;

		PatientPortalUser account = new(reader.GetString(0), reader.GetString(1));
		PasswordVerificationResult result =
			passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
		return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
			? account
			: null;
	}

	public async Task<PatientPortalAuthenticationState?> GetAuthenticationStateAsync(
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT account.Username
			FROM PatientPortalAccounts account
			WHERE account.Username = $Username
				AND EXISTS (
					SELECT 1 FROM PatientPortalIdentityLinks identityLink
					JOIN Patients patient ON patient.SharedPatientId = identityLink.SharedPatientId
					WHERE identityLink.Username = account.Username)
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());
		object? result = await command.ExecuteScalarAsync(cancellationToken);
		return result is string accountUsername
			? new PatientPortalAuthenticationState(accountUsername)
			: null;
	}

	public async Task<string?> CreateRegistrationCodeAsync(
		int patientId,
		string createdBy,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(createdBy) ||
			(!currentHospital.IsAdministrator && !currentHospital.IsEmployee))
		{
			return null;
		}

		string code = $"EH-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
		string? codeHash = HashRegistrationCode(code);
		if (codeHash is null)
			return null;

		DateTime now = DateTime.UtcNow;
		string nowText = FormatTimestamp(now);
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand patientCommand = connection.CreateCommand();
		patientCommand.Transaction = transaction;
		patientCommand.CommandText =
			"""
			SELECT EXISTS (
				SELECT 1 FROM Patients patient
				WHERE patient.PatientId = $PatientId
					AND patient.HospitalId = $HospitalId
					AND NOT EXISTS (
						SELECT 1 FROM PatientPortalIdentityLinks identityLink
						WHERE identityLink.SharedPatientId = patient.SharedPatientId))
			""";
		patientCommand.Parameters.AddWithValue("$PatientId", patientId);
		patientCommand.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		if (Convert.ToInt32(await patientCommand.ExecuteScalarAsync(cancellationToken)) != 1)
			return null;

		await using SqliteCommand revokeCommand = connection.CreateCommand();
		revokeCommand.Transaction = transaction;
		revokeCommand.CommandText =
			"""
			UPDATE PatientPortalRegistrationCodes
			SET RevokedAtUtc = $Now
			WHERE PatientId = $PatientId
				AND RedeemedAtUtc IS NULL AND RevokedAtUtc IS NULL
			""";
		revokeCommand.Parameters.AddWithValue("$Now", nowText);
		revokeCommand.Parameters.AddWithValue("$PatientId", patientId);
		await revokeCommand.ExecuteNonQueryAsync(cancellationToken);

		await using SqliteCommand createCommand = connection.CreateCommand();
		createCommand.Transaction = transaction;
		createCommand.CommandText =
			"""
			INSERT INTO PatientPortalRegistrationCodes (
				PatientId, CodeHash, CreatedBy, CreatedAtUtc, ExpiresAtUtc)
			VALUES ($PatientId, $CodeHash, $CreatedBy, $CreatedAtUtc, $ExpiresAtUtc)
			""";
		createCommand.Parameters.AddWithValue("$PatientId", patientId);
		createCommand.Parameters.AddWithValue("$CodeHash", codeHash);
		createCommand.Parameters.AddWithValue("$CreatedBy", createdBy.Trim());
		createCommand.Parameters.AddWithValue("$CreatedAtUtc", nowText);
		createCommand.Parameters.AddWithValue("$ExpiresAtUtc", FormatTimestamp(now.AddDays(7)));
		await createCommand.ExecuteNonQueryAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return code;
	}

	public async Task<PatientPortalRegistrationStatus> RegisterAsync(
		string registrationCode,
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		string normalizedUsername = username.Trim();
		string? codeHash = HashRegistrationCode(registrationCode);
		if (codeHash is null || !IsValidUsername(normalizedUsername) || password.Length < 12)
			return PatientPortalRegistrationStatus.InvalidCode;

		DateTime now = DateTime.UtcNow;
		string nowText = FormatTimestamp(now);
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand codeCommand = connection.CreateCommand();
		codeCommand.Transaction = transaction;
		codeCommand.CommandText =
			"""
			SELECT code.Id, patient.SharedPatientId
			FROM PatientPortalRegistrationCodes code
			JOIN Patients patient ON patient.PatientId = code.PatientId
			WHERE code.CodeHash = $CodeHash
				AND code.RedeemedAtUtc IS NULL
				AND code.RevokedAtUtc IS NULL
				AND code.ExpiresAtUtc > $Now
				AND NOT EXISTS (
					SELECT 1 FROM PatientPortalIdentityLinks identityLink
					WHERE identityLink.SharedPatientId = patient.SharedPatientId)
			""";
		codeCommand.Parameters.AddWithValue("$CodeHash", codeHash);
		codeCommand.Parameters.AddWithValue("$Now", nowText);
		await using SqliteDataReader codeReader = await codeCommand.ExecuteReaderAsync(cancellationToken);
		if (!await codeReader.ReadAsync(cancellationToken))
			return PatientPortalRegistrationStatus.InvalidCode;
		int codeId = codeReader.GetInt32(0);
		string sharedPatientId = codeReader.GetString(1);
		await codeReader.DisposeAsync();

		await using SqliteCommand usernameCommand = connection.CreateCommand();
		usernameCommand.Transaction = transaction;
		usernameCommand.CommandText =
			"""
			SELECT EXISTS (
				SELECT 1 FROM PatientPortalAccounts WHERE Username = $Username
				UNION ALL
				SELECT 1 FROM UserAccounts WHERE Username = $Username)
			""";
		usernameCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		if (Convert.ToInt32(await usernameCommand.ExecuteScalarAsync(cancellationToken)) != 0)
			return PatientPortalRegistrationStatus.UsernameUnavailable;

		PatientPortalUser account = new(normalizedUsername, string.Empty);
		string passwordHash = passwordHasher.HashPassword(account, password);
		await using SqliteCommand accountCommand = connection.CreateCommand();
		accountCommand.Transaction = transaction;
		accountCommand.CommandText =
			"""
			INSERT INTO PatientPortalAccounts (Username, PasswordHash, CreatedAtUtc)
			VALUES ($Username, $PasswordHash, $CreatedAtUtc)
			""";
		accountCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		accountCommand.Parameters.AddWithValue("$PasswordHash", passwordHash);
		accountCommand.Parameters.AddWithValue("$CreatedAtUtc", nowText);
		await accountCommand.ExecuteNonQueryAsync(cancellationToken);

		await using SqliteCommand identityLinkCommand = connection.CreateCommand();
		identityLinkCommand.Transaction = transaction;
		identityLinkCommand.CommandText =
			"""
			INSERT INTO PatientPortalIdentityLinks (SharedPatientId, Username, LinkedAtUtc)
			VALUES ($SharedPatientId, $Username, $LinkedAtUtc)
			""";
		identityLinkCommand.Parameters.AddWithValue("$SharedPatientId", sharedPatientId);
		identityLinkCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		identityLinkCommand.Parameters.AddWithValue("$LinkedAtUtc", nowText);
		await identityLinkCommand.ExecuteNonQueryAsync(cancellationToken);

		await using SqliteCommand redeemCommand = connection.CreateCommand();
		redeemCommand.Transaction = transaction;
		redeemCommand.CommandText =
			"""
			UPDATE PatientPortalRegistrationCodes
			SET RedeemedAtUtc = $Now, RedeemedBy = $Username
			WHERE Id = $Id AND RedeemedAtUtc IS NULL AND RevokedAtUtc IS NULL
				AND ExpiresAtUtc > $Now
			""";
		redeemCommand.Parameters.AddWithValue("$Now", nowText);
		redeemCommand.Parameters.AddWithValue("$Username", normalizedUsername);
		redeemCommand.Parameters.AddWithValue("$Id", codeId);
		if (await redeemCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return PatientPortalRegistrationStatus.InvalidCode;

		await transaction.CommitAsync(cancellationToken);
		return PatientPortalRegistrationStatus.Succeeded;
	}

	public async Task<IReadOnlyList<PatientPortalRecord>> GetRecordsAsync(
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT p.PatientId, p.HospitalId, hospital.Name, p.FirstName, p.LastName,
				p.AdmissionDate, p.DischargeDate, p.Gender, p.DateOfBirth, p.Address,
				p.Phone, p.Status, p.EmergencyContactName, p.EmergencyContactPhone,
				p.SharedPatientId
			FROM Patients p
			JOIN Hospitals hospital ON hospital.Id = p.HospitalId
			WHERE EXISTS (
				SELECT 1 FROM PatientPortalIdentityLinks identityLink
				WHERE identityLink.Username = $Username
					AND identityLink.SharedPatientId = p.SharedPatientId)
			ORDER BY hospital.Name, p.PatientId
			""";
		command.Parameters.AddWithValue("$Username", username.Trim());

		List<(PatientSummary Summary, PatientRecord Details)> linkedPatients = [];
		await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
		{
			while (await reader.ReadAsync(cancellationToken))
			{
				PatientSummary summary = new(
					reader.GetInt32(0),
					reader.GetInt32(1),
					reader.GetString(2),
					reader.GetString(3),
					reader.GetString(4),
					ParseDate(reader.GetString(5)),
					reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)));
				PatientRecord details = new(
					reader.GetInt32(0),
					reader.GetInt32(1),
					reader.GetString(3),
					reader.GetString(4),
					reader.GetString(7),
					ParseDate(reader.GetString(8)),
					reader.GetString(9),
					reader.GetString(10),
					ParseDate(reader.GetString(5)),
					reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
					reader.GetInt32(11),
					reader.GetString(12),
					reader.GetString(13),
					reader.GetString(14));
				linkedPatients.Add((summary, details));
			}
		}

		List<PatientPortalRecord> records = [];
		foreach ((PatientSummary summary, PatientRecord details) in linkedPatients)
		{
			IReadOnlyList<PatientPortalVisit> visits =
				await GetVisitsAsync(connection, summary.PatientId, cancellationToken);
			IReadOnlyList<PatientPortalTest> tests =
				await GetTestsAsync(connection, summary.PatientId, cancellationToken);
			records.Add(new PatientPortalRecord(summary, details, visits, tests));
		}

		return records;
	}

	private static async Task<IReadOnlyList<PatientPortalVisit>> GetVisitsAsync(
		SqliteConnection connection,
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT checkup.CheckupId, checkup.PatientId, checkup.HospitalId,
				hospital.Name, checkup.CheckupDate, checkup.Reason, checkup.Notes,
				checkup.MedicalHistoryJson, checkup.FollowUpDate, checkup.CreatedBy,
				checkup.CreatedAtUtc, checkup.UpdatedBy, checkup.UpdatedAtUtc,
				COALESCE(NULLIF(trim(COALESCE(creator.FirstName, '') || ' ' || COALESCE(creator.LastName, '')), ''),
					checkup.CreatedBy),
				COALESCE(NULLIF(trim(COALESCE(updater.FirstName, '') || ' ' || COALESCE(updater.LastName, '')), ''),
					checkup.UpdatedBy)
			FROM PatientCheckups checkup
			JOIN Hospitals hospital ON hospital.Id = checkup.HospitalId
			LEFT JOIN UserAccounts creator ON creator.Username = checkup.CreatedBy COLLATE NOCASE
			LEFT JOIN UserAccounts updater ON updater.Username = checkup.UpdatedBy COLLATE NOCASE
			WHERE checkup.PatientId = $PatientId
			ORDER BY checkup.CheckupDate DESC, checkup.CheckupId DESC
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientPortalVisit> visits = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			PatientCheckup checkup = new(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetInt32(2),
				reader.GetString(3),
				ParseDate(reader.GetString(4)),
				ReadMedicalHistory(reader.GetString(7), reader.GetString(5), reader.GetString(6)),
				reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8)),
				reader.GetString(9),
				ParseTimestamp(reader.GetString(10)),
				reader.GetString(11),
				ParseTimestamp(reader.GetString(12)));
			visits.Add(new PatientPortalVisit(checkup, reader.GetString(13), reader.GetString(14)));
		}

		return visits;
	}

	private static async Task<IReadOnlyList<PatientPortalTest>> GetTestsAsync(
		SqliteConnection connection,
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT result.TestResultId, result.PatientId, result.HospitalId,
				result.CheckupId, result.TestDate, result.TestName, result.Result,
				result.Unit, result.ReferenceRange, result.Notes, result.CreatedBy,
				result.CreatedAtUtc, result.UpdatedBy, result.UpdatedAtUtc, hospital.Name,
				COALESCE(NULLIF(trim(COALESCE(creator.FirstName, '') || ' ' || COALESCE(creator.LastName, '')), ''),
					result.CreatedBy),
				COALESCE(NULLIF(trim(COALESCE(updater.FirstName, '') || ' ' || COALESCE(updater.LastName, '')), ''),
					result.UpdatedBy)
			FROM PatientTestResults result
			JOIN Hospitals hospital ON hospital.Id = result.HospitalId
			LEFT JOIN UserAccounts creator ON creator.Username = result.CreatedBy COLLATE NOCASE
			LEFT JOIN UserAccounts updater ON updater.Username = result.UpdatedBy COLLATE NOCASE
			WHERE result.PatientId = $PatientId
			ORDER BY result.TestDate DESC, result.TestResultId DESC
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientPortalTest> tests = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			PatientTestResult result = new(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetInt32(2),
				reader.IsDBNull(3) ? null : reader.GetInt32(3),
				ParseDate(reader.GetString(4)),
				reader.GetString(5),
				reader.GetString(6),
				reader.GetString(7),
				reader.GetString(8),
				reader.GetString(9),
				reader.GetString(10),
				ParseTimestamp(reader.GetString(11)),
				reader.GetString(12),
				ParseTimestamp(reader.GetString(13)));
			tests.Add(new PatientPortalTest(
				result,
				reader.GetString(14),
				reader.GetString(15),
				reader.GetString(16)));
		}

		return tests;
	}

	private static PatientMedicalHistory ReadMedicalHistory(
		string json,
		string legacyReason,
		string legacyNotes)
	{
		PatientMedicalHistory history = JsonSerializer.Deserialize<PatientMedicalHistory>(json)
			?? throw new InvalidDataException("A patient check-up contains invalid medical history data.");
		if (json == "{}")
		{
			history.ChiefComplaint = legacyReason;
			history.AdditionalNotes = legacyNotes;
		}

		return history;
	}

	private static string? HashRegistrationCode(string code)
	{
		string normalized = new(code
			.Where(char.IsAsciiLetterOrDigit)
			.Select(char.ToUpperInvariant)
			.ToArray());
		if (normalized.StartsWith("EH", StringComparison.Ordinal))
			normalized = normalized[2..];
		if (normalized.Length != 32 || !normalized.All(Uri.IsHexDigit))
			return null;

		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
	}

	private static bool IsValidUsername(string username) =>
		username.Length is >= 3 and <= 64 &&
		char.IsAsciiLetterOrDigit(username[0]) &&
		username.All(character =>
			char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

	private static string FormatTimestamp(DateTime value) =>
		value.ToString("O", CultureInfo.InvariantCulture);

	private static DateTime ParseDate(string value) =>
		DateTime.Parse(value, CultureInfo.InvariantCulture);

	private static DateTime ParseTimestamp(string value) =>
		DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
