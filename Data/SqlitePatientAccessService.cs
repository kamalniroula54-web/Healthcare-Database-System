using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class SqlitePatientAccessService(
	LocalDatabase database,
	CurrentHospitalContext currentHospital) : IPatientAccessService
{
	public async Task<IReadOnlyList<HospitalSummary>> GetRequestableHospitalsAsync(
		CancellationToken cancellationToken)
	{
		EnsureEmployee();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT Id, Name FROM Hospitals WHERE IsActive = 1 AND Id != $HospitalId ORDER BY Name";
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<HospitalSummary> hospitals = [];
		while (await reader.ReadAsync(cancellationToken))
			hospitals.Add(new HospitalSummary(reader.GetInt32(0), reader.GetString(1)));
		return hospitals;
	}

	public async Task<IReadOnlyList<PatientAccessRequestSummary>> GetEmployeeRequestsAsync(
		CancellationToken cancellationToken)
	{
		EnsureEmployee();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = RequestSelect + Environment.NewLine + """
			WHERE request.EmployeeUsername = $Username
			ORDER BY request.RequestedAtUtc DESC, request.Id DESC
			""";
		command.Parameters.AddWithValue("$Username", currentHospital.Username);
		return await ReadRequestsAsync(command, cancellationToken);
	}

	public async Task<bool> RequestAccessAsync(
		int targetHospitalId,
		string patientReference,
		string reason,
		CancellationToken cancellationToken)
	{
		EnsureEmployee();
		if (targetHospitalId == currentHospital.HospitalId ||
			string.IsNullOrWhiteSpace(patientReference) ||
			string.IsNullOrWhiteSpace(reason) ||
			patientReference.Trim().Length > 200 ||
			reason.Trim().Length > 1000)
		{
			return false;
		}

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			INSERT INTO PatientAccessRequests (
				EmployeeUsername, EmployeeHospitalId, TargetHospitalId,
				PatientReference, Reason, RequestedAtUtc)
			SELECT $Username, $EmployeeHospitalId, $TargetHospitalId,
				$PatientReference, $Reason, $RequestedAtUtc
			WHERE EXISTS (
				SELECT 1 FROM Hospitals WHERE Id = $TargetHospitalId AND IsActive = 1)
				AND NOT EXISTS (
					SELECT 1 FROM PatientAccessRequests
					WHERE EmployeeUsername = $Username COLLATE NOCASE
						AND TargetHospitalId = $TargetHospitalId
						AND PatientReference = $PatientReference COLLATE NOCASE
						AND Status = 'Pending')
			""";
		command.Parameters.AddWithValue("$Username", currentHospital.Username);
		command.Parameters.AddWithValue("$EmployeeHospitalId", currentHospital.HospitalId);
		command.Parameters.AddWithValue("$TargetHospitalId", targetHospitalId);
		command.Parameters.AddWithValue("$PatientReference", patientReference.Trim());
		command.Parameters.AddWithValue("$Reason", reason.Trim());
		command.Parameters.AddWithValue("$RequestedAtUtc", UtcNow());
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<IReadOnlyList<PatientAccessRequestSummary>> GetAdminRequestsAsync(
		CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = RequestSelect + Environment.NewLine +
			(currentHospital.IsAdministrator ? string.Empty : "WHERE request.TargetHospitalId = $HospitalId\n") +
			"""
			ORDER BY CASE request.Status WHEN 'Pending' THEN 0 ELSE 1 END,
				request.RequestedAtUtc DESC, request.Id DESC
			""";
		if (!currentHospital.IsAdministrator)
			command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await ReadRequestsAsync(command, cancellationToken);
	}

	public async Task<IReadOnlyList<PatientCandidate>> GetPatientCandidatesAsync(
		int hospitalId,
		CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		if (!currentHospital.IsAdministrator && hospitalId != currentHospital.HospitalId)
			throw new UnauthorizedAccessException("Hospital administrators can only match patients at their assigned hospital.");

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT PatientId, FirstName || ' ' || LastName
			FROM Patients
			WHERE HospitalId = $HospitalId
			ORDER BY LastName, FirstName, PatientId
			""";
		command.Parameters.AddWithValue("$HospitalId", hospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientCandidate> patients = [];
		while (await reader.ReadAsync(cancellationToken))
			patients.Add(new PatientCandidate(reader.GetInt32(0), reader.GetString(1)));
		return patients;
	}

	public async Task<bool> ApproveRequestAsync(
		int requestId,
		int patientId,
		CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand requestCommand = connection.CreateCommand();
		requestCommand.Transaction = transaction;
		requestCommand.CommandText =
			"SELECT TargetHospitalId FROM PatientAccessRequests " +
			"WHERE Id = $Id AND Status = 'Pending' " +
			"AND ($IsGlobalAdmin = 1 OR TargetHospitalId = $HospitalId)";
		requestCommand.Parameters.AddWithValue("$Id", requestId);
		requestCommand.Parameters.AddWithValue("$IsGlobalAdmin", currentHospital.IsAdministrator);
		requestCommand.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		object? targetValue = await requestCommand.ExecuteScalarAsync(cancellationToken);
		if (targetValue is null)
			return false;
		int targetHospitalId = Convert.ToInt32(targetValue, CultureInfo.InvariantCulture);

		await using SqliteCommand grantCommand = connection.CreateCommand();
		grantCommand.Transaction = transaction;
		grantCommand.CommandText =
			"""
			INSERT INTO PatientAccessGrants (
				RequestId, EmployeeUsername, TargetHospitalId, PatientId, GrantedBy, GrantedAtUtc)
			SELECT request.Id, request.EmployeeUsername, request.TargetHospitalId,
				patient.PatientId, $GrantedBy, $GrantedAtUtc
			FROM PatientAccessRequests request
			JOIN UserAccounts employee
				ON employee.Username = request.EmployeeUsername COLLATE NOCASE
				AND employee.Role = 'Employee'
				AND employee.HospitalId = request.EmployeeHospitalId
			JOIN Patients patient
				ON patient.PatientId = $PatientId AND patient.HospitalId = request.TargetHospitalId
			WHERE request.Id = $RequestId AND request.Status = 'Pending'
			""";
		grantCommand.Parameters.AddWithValue("$GrantedBy", currentHospital.Username);
		grantCommand.Parameters.AddWithValue("$GrantedAtUtc", UtcNow());
		grantCommand.Parameters.AddWithValue("$PatientId", patientId);
		grantCommand.Parameters.AddWithValue("$RequestId", requestId);
		if (await grantCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return false;

		await using SqliteCommand resolveCommand = connection.CreateCommand();
		resolveCommand.Transaction = transaction;
		resolveCommand.CommandText =
			"""
			UPDATE PatientAccessRequests
			SET Status = 'Approved', ResolvedBy = $ResolvedBy,
				ResolvedAtUtc = $ResolvedAtUtc, GrantedPatientId = $PatientId
			WHERE Id = $RequestId AND Status = 'Pending'
				AND TargetHospitalId = $TargetHospitalId
			""";
		resolveCommand.Parameters.AddWithValue("$ResolvedBy", currentHospital.Username);
		resolveCommand.Parameters.AddWithValue("$ResolvedAtUtc", UtcNow());
		resolveCommand.Parameters.AddWithValue("$PatientId", patientId);
		resolveCommand.Parameters.AddWithValue("$RequestId", requestId);
		resolveCommand.Parameters.AddWithValue("$TargetHospitalId", targetHospitalId);
		if (await resolveCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
			return false;

		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<bool> RejectRequestAsync(int requestId, CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE PatientAccessRequests
			SET Status = 'Rejected', ResolvedBy = $ResolvedBy, ResolvedAtUtc = $ResolvedAtUtc
			WHERE Id = $Id AND Status = 'Pending'
				AND ($IsGlobalAdmin = 1 OR TargetHospitalId = $HospitalId)
			""";
		command.Parameters.AddWithValue("$ResolvedBy", currentHospital.Username);
		command.Parameters.AddWithValue("$ResolvedAtUtc", UtcNow());
		command.Parameters.AddWithValue("$Id", requestId);
		command.Parameters.AddWithValue("$IsGlobalAdmin", currentHospital.IsAdministrator);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<IReadOnlyList<PatientAccessGrantSummary>> GetEmployeeGrantsAsync(
		CancellationToken cancellationToken)
	{
		EnsureEmployee();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = GrantSelect + Environment.NewLine + """
			WHERE accessRequest.EmployeeHospitalId = $HospitalId
			ORDER BY accessGrant.GrantedAtUtc DESC, accessGrant.Id DESC
			""";
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await ReadGrantsAsync(command, cancellationToken);
	}

	public async Task<IReadOnlyList<PatientAccessGrantSummary>> GetAdminGrantsAsync(
		CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = GrantSelect + Environment.NewLine +
			(currentHospital.IsAdministrator ? string.Empty : "WHERE accessGrant.TargetHospitalId = $HospitalId\n") +
			"ORDER BY accessGrant.RevokedAtUtc IS NULL DESC, accessGrant.GrantedAtUtc DESC";
		if (!currentHospital.IsAdministrator)
			command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await ReadGrantsAsync(command, cancellationToken);
	}

	public async Task<bool> RevokeGrantAsync(int grantId, CancellationToken cancellationToken)
	{
		EnsureAdministrator();
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE PatientAccessGrants
			SET RevokedBy = $RevokedBy, RevokedAtUtc = $RevokedAtUtc
			WHERE Id IN (
				SELECT relatedGrant.Id
				FROM PatientAccessGrants selectedGrant
				JOIN PatientAccessRequests selectedRequest
					ON selectedRequest.Id = selectedGrant.RequestId
				JOIN PatientAccessGrants relatedGrant
					ON relatedGrant.PatientId = selectedGrant.PatientId
					AND relatedGrant.TargetHospitalId = selectedGrant.TargetHospitalId
				JOIN PatientAccessRequests relatedRequest
					ON relatedRequest.Id = relatedGrant.RequestId
					AND relatedRequest.EmployeeHospitalId = selectedRequest.EmployeeHospitalId
				WHERE selectedGrant.Id = $Id
					AND selectedGrant.RevokedAtUtc IS NULL
					AND ($IsGlobalAdmin = 1 OR selectedGrant.TargetHospitalId = $HospitalId)
					AND relatedGrant.RevokedAtUtc IS NULL)
			""";
		command.Parameters.AddWithValue("$RevokedBy", currentHospital.Username);
		command.Parameters.AddWithValue("$RevokedAtUtc", UtcNow());
		command.Parameters.AddWithValue("$Id", grantId);
		command.Parameters.AddWithValue("$IsGlobalAdmin", currentHospital.IsAdministrator);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private const string RequestSelect =
		"""
		SELECT request.Id, request.EmployeeUsername,
			trim(coalesce(employee.FirstName, '') || ' ' || coalesce(employee.LastName, '')),
			employeeHospital.Name, request.TargetHospitalId, targetHospital.Name,
			request.PatientReference, request.Reason, request.RequestedAtUtc,
			request.Status, request.ResolvedBy, request.ResolvedAtUtc,
			request.GrantedPatientId,
			CASE WHEN grantedPatient.PatientId IS NULL THEN NULL
				ELSE grantedPatient.FirstName || ' ' || grantedPatient.LastName END
		FROM PatientAccessRequests request
		JOIN Hospitals employeeHospital ON employeeHospital.Id = request.EmployeeHospitalId
		JOIN Hospitals targetHospital ON targetHospital.Id = request.TargetHospitalId
		JOIN UserAccounts employee ON employee.Username = request.EmployeeUsername COLLATE NOCASE
		LEFT JOIN Patients grantedPatient ON grantedPatient.PatientId = request.GrantedPatientId
		""";

	private const string GrantSelect =
		"""
		SELECT accessGrant.Id, accessGrant.EmployeeUsername,
			trim(coalesce(employee.FirstName, '') || ' ' || coalesce(employee.LastName, '')),
			accessGrant.TargetHospitalId, sourceHospital.Name,
			accessRequest.EmployeeHospitalId, receivingHospital.Name,
			accessGrant.PatientId, patient.FirstName || ' ' || patient.LastName,
			accessGrant.GrantedBy, accessGrant.GrantedAtUtc,
			accessGrant.RevokedBy, accessGrant.RevokedAtUtc
		FROM PatientAccessGrants accessGrant
		JOIN PatientAccessRequests accessRequest ON accessRequest.Id = accessGrant.RequestId
		JOIN Hospitals sourceHospital ON sourceHospital.Id = accessGrant.TargetHospitalId
		JOIN Hospitals receivingHospital ON receivingHospital.Id = accessRequest.EmployeeHospitalId
		JOIN Patients patient ON patient.PatientId = accessGrant.PatientId
		LEFT JOIN UserAccounts employee ON employee.Username = accessGrant.EmployeeUsername COLLATE NOCASE
		""";

	private static async Task<IReadOnlyList<PatientAccessRequestSummary>> ReadRequestsAsync(
		SqliteCommand command,
		CancellationToken cancellationToken)
	{
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientAccessRequestSummary> requests = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			requests.Add(new PatientAccessRequestSummary(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetString(2).Trim(),
				reader.GetString(3),
				reader.GetInt32(4),
				reader.GetString(5),
				reader.GetString(6),
				reader.GetString(7),
				ParseTimestamp(reader.GetString(8)),
				reader.GetString(9),
				reader.IsDBNull(10) ? null : reader.GetString(10),
				reader.IsDBNull(11) ? null : ParseTimestamp(reader.GetString(11)),
				reader.IsDBNull(12) ? null : reader.GetInt32(12),
				reader.IsDBNull(13) ? null : reader.GetString(13)));
		}

		return requests;
	}

	private static async Task<IReadOnlyList<PatientAccessGrantSummary>> ReadGrantsAsync(
		SqliteCommand command,
		CancellationToken cancellationToken)
	{
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientAccessGrantSummary> grants = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			grants.Add(new PatientAccessGrantSummary(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetString(2).Trim(),
				reader.GetInt32(3),
				reader.GetString(4),
				reader.GetInt32(5),
				reader.GetString(6),
				reader.GetInt32(7),
				reader.GetString(8),
				reader.GetString(9),
				ParseTimestamp(reader.GetString(10)),
				reader.IsDBNull(11) ? null : reader.GetString(11),
				reader.IsDBNull(12) ? null : ParseTimestamp(reader.GetString(12))));
		}

		return grants;
	}

	private void EnsureAdministrator()
	{
		if (!currentHospital.CanApprovePatientAccess)
			throw new UnauthorizedAccessException(
				"Patient access approvals require a global administrator or delegated hospital administrator.");
	}

	private void EnsureEmployee()
	{
		if (!currentHospital.IsEmployee)
			throw new UnauthorizedAccessException("Patient data requests require an employee account.");
	}

	private static string UtcNow() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

	private static DateTime ParseTimestamp(string timestamp) =>
		DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
