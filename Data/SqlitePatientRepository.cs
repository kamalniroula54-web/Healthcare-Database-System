using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class SqlitePatientRepository(
	LocalDatabase database,
	CurrentHospitalContext currentHospital) : IPatientRepository
{
	private const string SelectColumns =
		"PatientId, HospitalId, FirstName, LastName, Gender, DateOfBirth, Address, Phone, AdmissionDate, DischargeDate, Status, " +
		"EmergencyContactName, EmergencyContactPhone, SharedPatientId";

	public async Task<IReadOnlyList<PatientSummary>> GetSummariesAsync(CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT p.PatientId, p.HospitalId, hospital.Name, p.FirstName, p.LastName,
				p.AdmissionDate, p.DischargeDate
			FROM Patients p
			JOIN Hospitals hospital ON hospital.Id = p.HospitalId
			WHERE {GetPatientReadPredicate()}
			ORDER BY CASE WHEN p.HospitalId = $HospitalId THEN 0 ELSE 1 END, p.PatientId DESC
			""";
		AddReadAccessParameters(command);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

		List<PatientSummary> patients = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			patients.Add(new PatientSummary(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				ParseDate(reader.GetString(5)),
				reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6))));
		}

		return patients;
	}

	public async Task<PatientSummary?> GetSummaryByIdAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT p.PatientId, p.HospitalId, hospital.Name, p.FirstName, p.LastName,
				p.AdmissionDate, p.DischargeDate
			FROM Patients p
			JOIN Hospitals hospital ON hospital.Id = p.HospitalId
			WHERE p.PatientId = $PatientId AND {GetPatientReadPredicate()}
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		AddReadAccessParameters(command);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new PatientSummary(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				ParseDate(reader.GetString(5)),
				reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)))
			: null;
	}

	public async Task<PatientRecord?> GetByIdAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT {SelectColumns}
			FROM Patients p
			WHERE p.PatientId = $PatientId AND {GetPatientReadPredicate()}
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		AddReadAccessParameters(command);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
	}

	public async Task<PatientDateUpdate?> GetDatesByIdAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT AdmissionDate, DischargeDate FROM Patients WHERE PatientId = $PatientId AND HospitalId = $HospitalId";
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new PatientDateUpdate
			{
				AdmissionDate = ParseDate(reader.GetString(0)),
				DischargeDate = reader.IsDBNull(1) ? null : ParseDate(reader.GetString(1))
			}
			: null;
	}

	public async Task CreateAsync(PatientForm patient, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"INSERT INTO Patients (HospitalId, FirstName, LastName, Gender, DateOfBirth, Address, Phone, " +
			"EmergencyContactName, EmergencyContactPhone, SharedPatientId, AdmissionDate, DischargeDate, Status) " +
			"VALUES ($HospitalId, $FirstName, $LastName, $Gender, $DateOfBirth, $Address, $Phone, " +
			"$EmergencyContactName, $EmergencyContactPhone, $SharedPatientId, $AdmissionDate, $DischargeDate, $Status)";
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		AddPatientParameters(command, patient);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<bool> UpdateAsync(int patientId, PatientForm patient, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"UPDATE Patients SET FirstName=$FirstName, LastName=$LastName, Gender=$Gender, DateOfBirth=$DateOfBirth, " +
			"Address=$Address, Phone=$Phone, EmergencyContactName=$EmergencyContactName, " +
			"EmergencyContactPhone=$EmergencyContactPhone, SharedPatientId=$SharedPatientId, AdmissionDate=$AdmissionDate, " +
			"DischargeDate=$DischargeDate, Status=$Status " +
			"WHERE PatientId=$PatientId AND HospitalId=$HospitalId";
		AddPatientParameters(command, patient);
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<bool> UpdateDatesAsync(
		int patientId,
		PatientDateUpdate dates,
		string changedBy,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();

		await using SqliteCommand selectCommand = connection.CreateCommand();
		selectCommand.Transaction = transaction;
		selectCommand.CommandText =
			"SELECT AdmissionDate, DischargeDate FROM Patients WHERE PatientId = $PatientId AND HospitalId = $HospitalId";
		selectCommand.Parameters.AddWithValue("$PatientId", patientId);
		selectCommand.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		await using SqliteDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return false;

		DateTime previousAdmissionDate = ParseDate(reader.GetString(0));
		DateTime? previousDischargeDate = reader.IsDBNull(1) ? null : ParseDate(reader.GetString(1));
		await reader.DisposeAsync();

		await using SqliteCommand updateCommand = connection.CreateCommand();
		updateCommand.Transaction = transaction;
		updateCommand.CommandText =
			"UPDATE Patients SET AdmissionDate = $AdmissionDate, DischargeDate = $DischargeDate " +
			"WHERE PatientId = $PatientId AND HospitalId = $HospitalId";
		updateCommand.Parameters.AddWithValue("$AdmissionDate", FormatDate(dates.AdmissionDate));
		updateCommand.Parameters.Add("$DischargeDate", SqliteType.Text).Value =
			dates.DischargeDate.HasValue ? FormatDate(dates.DischargeDate.Value) : DBNull.Value;
		updateCommand.Parameters.AddWithValue("$PatientId", patientId);
		updateCommand.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		await updateCommand.ExecuteNonQueryAsync(cancellationToken);

		bool changed = previousAdmissionDate.Date != dates.AdmissionDate.Date ||
			previousDischargeDate?.Date != dates.DischargeDate?.Date;
		if (changed)
		{
			await using SqliteCommand auditCommand = connection.CreateCommand();
			auditCommand.Transaction = transaction;
			auditCommand.CommandText =
				"""
				INSERT INTO PatientDateChanges (
					PatientId, ChangedBy, PreviousAdmissionDate, NewAdmissionDate,
					PreviousDischargeDate, NewDischargeDate, ChangedAtUtc)
				VALUES (
					$PatientId, $ChangedBy, $PreviousAdmissionDate, $NewAdmissionDate,
					$PreviousDischargeDate, $NewDischargeDate, $ChangedAtUtc)
				""";
			auditCommand.Parameters.AddWithValue("$PatientId", patientId);
			auditCommand.Parameters.AddWithValue("$ChangedBy", changedBy);
			auditCommand.Parameters.AddWithValue("$PreviousAdmissionDate", FormatDate(previousAdmissionDate));
			auditCommand.Parameters.AddWithValue("$NewAdmissionDate", FormatDate(dates.AdmissionDate));
			auditCommand.Parameters.Add("$PreviousDischargeDate", SqliteType.Text).Value =
				previousDischargeDate.HasValue ? FormatDate(previousDischargeDate.Value) : DBNull.Value;
			auditCommand.Parameters.Add("$NewDischargeDate", SqliteType.Text).Value =
				dates.DischargeDate.HasValue ? FormatDate(dates.DischargeDate.Value) : DBNull.Value;
			auditCommand.Parameters.AddWithValue("$ChangedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
			await auditCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<bool> DeleteAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "DELETE FROM Patients WHERE PatientId=$PatientId AND HospitalId=$HospitalId";
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<IReadOnlyList<PatientDateChange>> GetUnacknowledgedDateChangesAsync(
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT change.Id, change.PatientId, change.ChangedBy,
				change.PreviousAdmissionDate, change.NewAdmissionDate,
				PreviousDischargeDate, NewDischargeDate, ChangedAtUtc
			FROM PatientDateChanges change
			JOIN Patients patient ON patient.PatientId = change.PatientId
			WHERE change.IsAcknowledged = 0 AND patient.HospitalId = $HospitalId
			ORDER BY change.ChangedAtUtc DESC
			""";
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientDateChange> changes = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			changes.Add(new PatientDateChange(
				reader.GetInt32(0),
				reader.GetInt32(1),
				reader.GetString(2),
				ParseDate(reader.GetString(3)),
				ParseDate(reader.GetString(4)),
				reader.IsDBNull(5) ? null : ParseDate(reader.GetString(5)),
				reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
				DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
		}

		return changes;
	}

	public async Task<bool> AcknowledgeDateChangeAsync(int changeId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE PatientDateChanges
			SET IsAcknowledged = 1
			WHERE Id = $Id AND IsAcknowledged = 0 AND EXISTS (
				SELECT 1 FROM Patients patient
				WHERE patient.PatientId = PatientDateChanges.PatientId
					AND patient.HospitalId = $HospitalId)
			""";
		command.Parameters.AddWithValue("$Id", changeId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<IReadOnlyList<PatientCheckup>> GetCheckupsAsync(
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT checkup.CheckupId, checkup.PatientId, checkup.HospitalId,
				encounterHospital.Name, checkup.CheckupDate,
				checkup.Reason, checkup.Notes, checkup.MedicalHistoryJson, checkup.FollowUpDate,
				checkup.CreatedBy, checkup.CreatedAtUtc, checkup.UpdatedBy, checkup.UpdatedAtUtc
			FROM PatientCheckups checkup
			JOIN Patients p ON p.PatientId = checkup.PatientId
			JOIN Hospitals encounterHospital ON encounterHospital.Id = checkup.HospitalId
			WHERE checkup.PatientId = $PatientId
				AND {GetPatientReadPredicate()}
			ORDER BY checkup.CheckupDate DESC, checkup.CheckupId DESC
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		AddReadAccessParameters(command);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientCheckup> checkups = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			checkups.Add(new PatientCheckup(
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
				ParseTimestamp(reader.GetString(12))));
		}

		return checkups;
	}

	public async Task<bool> SaveCheckupAsync(
		int patientId,
		PatientCheckupForm checkup,
		string username,
		CancellationToken cancellationToken)
	{
		if (checkup.Tests.Count > PatientCheckupForm.MaxTestsPerVisit)
			return false;

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();
		if (!await CanReadPatientAsync(connection, transaction, patientId, cancellationToken))
			return false;

		string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
		int checkupId;
		if (checkup.Id == 0)
		{
			await using SqliteCommand insertCommand = connection.CreateCommand();
			insertCommand.Transaction = transaction;
			insertCommand.CommandText =
				"""
				INSERT INTO PatientCheckups (
					PatientId, HospitalId, CheckupDate, Reason, Notes, MedicalHistoryJson, FollowUpDate,
					CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc)
				VALUES (
					$PatientId, $HospitalId, $CheckupDate, $Reason, $Notes, $MedicalHistoryJson, $FollowUpDate,
					$Username, $Now, $Username, $Now)
				""";
			AddCheckupParameters(insertCommand, patientId, checkup, username, now);
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);

			await using SqliteCommand idCommand = connection.CreateCommand();
			idCommand.Transaction = transaction;
			idCommand.CommandText = "SELECT last_insert_rowid()";
			checkupId = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
		}
		else
		{
			await using SqliteCommand updateCommand = connection.CreateCommand();
			updateCommand.Transaction = transaction;
			updateCommand.CommandText =
				"""
				UPDATE PatientCheckups
				SET CheckupDate = $CheckupDate, Reason = $Reason, Notes = $Notes,
					MedicalHistoryJson = $MedicalHistoryJson, FollowUpDate = $FollowUpDate,
					UpdatedBy = $Username, UpdatedAtUtc = $Now
				WHERE PatientId = $PatientId AND CheckupId = $CheckupId
						AND PatientCheckups.HospitalId = $HospitalId
				""";
			updateCommand.Parameters.AddWithValue("$CheckupId", checkup.Id);
			AddCheckupParameters(updateCommand, patientId, checkup, username, now);
			if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
				return false;
			checkupId = checkup.Id;
		}

		if (!await SaveVisitTestResultsAsync(
			connection,
			transaction,
			patientId,
			checkupId,
			checkup.Tests,
			username,
			now,
			currentHospital.HospitalId,
			cancellationToken))
		{
			return false;
		}

		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<bool> DeleteCheckupAsync(
		int patientId,
		int checkupId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			DELETE FROM PatientCheckups
			WHERE PatientId = $PatientId AND CheckupId = $CheckupId
				AND HospitalId = $HospitalId
				AND EXISTS (
					SELECT 1 FROM Patients p
					WHERE p.PatientId = PatientCheckups.PatientId
						AND {GetPatientReadPredicate()})
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$CheckupId", checkupId);
		AddReadAccessParameters(command);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<IReadOnlyList<PatientTestResult>> GetTestResultsAsync(
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"""
			SELECT result.TestResultId, result.PatientId, result.HospitalId,
				result.CheckupId, result.TestDate,
				result.TestName, result.Result, result.Unit, result.ReferenceRange,
				result.Notes, result.CreatedBy, result.CreatedAtUtc, result.UpdatedBy, result.UpdatedAtUtc
			FROM PatientTestResults result
			JOIN Patients p ON p.PatientId = result.PatientId
			WHERE result.PatientId = $PatientId
				AND {GetPatientReadPredicate()}
			ORDER BY result.TestDate DESC, result.TestResultId DESC
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		AddReadAccessParameters(command);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<PatientTestResult> results = [];
		while (await reader.ReadAsync(cancellationToken))
		{
			results.Add(new PatientTestResult(
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
				ParseTimestamp(reader.GetString(13))));
		}

		return results;
	}

	public async Task<bool> SaveTestResultAsync(
		int patientId,
		PatientTestResultForm result,
		string username,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		if (!await IsLocalPatientAsync(connection, null, patientId, cancellationToken))
			return false;

		await using SqliteCommand command = connection.CreateCommand();
		string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
		if (result.Id == 0)
		{
			command.CommandText =
				"""
				INSERT INTO PatientTestResults (
					PatientId, HospitalId, TestDate, TestName, Result, Unit, ReferenceRange, Notes,
					CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc)
				VALUES (
					$PatientId, $HospitalId, $TestDate, $TestName, $Result, $Unit, $ReferenceRange, $Notes,
					$Username, $Now, $Username, $Now)
				""";
		}
		else
		{
			command.CommandText =
				"""
				UPDATE PatientTestResults
				SET TestDate = $TestDate, TestName = $TestName, Result = $Result,
					Unit = $Unit, ReferenceRange = $ReferenceRange, Notes = $Notes,
					UpdatedBy = $Username, UpdatedAtUtc = $Now
				WHERE PatientId = $PatientId AND TestResultId = $TestResultId
					AND HospitalId = $HospitalId AND CheckupId IS NULL
				""";
			command.Parameters.AddWithValue("$TestResultId", result.Id);
		}

		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		AddTestResultParameters(command, result, username, now);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<bool> DeleteTestResultAsync(
		int patientId,
		int testResultId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			DELETE FROM PatientTestResults
			WHERE PatientId = $PatientId AND TestResultId = $TestResultId AND CheckupId IS NULL
				AND EXISTS (
					SELECT 1 FROM Patients
					WHERE Patients.PatientId = PatientTestResults.PatientId
						AND Patients.HospitalId = $HospitalId)
				AND PatientTestResults.HospitalId = $HospitalId
			""";
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$TestResultId", testResultId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	private static string GetPatientReadPredicate() =>
		"""
		(p.HospitalId = $HospitalId OR EXISTS (
			SELECT 1 FROM PatientAccessGrants accessGrant
			JOIN PatientAccessRequests accessRequest
				ON accessRequest.Id = accessGrant.RequestId
			WHERE accessRequest.EmployeeHospitalId = $HospitalId
				AND accessGrant.TargetHospitalId = p.HospitalId
				AND accessGrant.PatientId = p.PatientId
				AND accessGrant.RevokedAtUtc IS NULL))
		""";

	private void AddReadAccessParameters(SqliteCommand command)
	{
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
	}

	private async Task<bool> IsLocalPatientAsync(
		SqliteConnection connection,
		SqliteTransaction? transaction,
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			"SELECT EXISTS(SELECT 1 FROM Patients WHERE PatientId = $PatientId AND HospitalId = $HospitalId)";
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
	}

	private async Task<bool> CanReadPatientAsync(
		SqliteConnection connection,
		SqliteTransaction? transaction,
		int patientId,
		CancellationToken cancellationToken)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText =
			$"SELECT EXISTS(SELECT 1 FROM Patients p WHERE p.PatientId = $PatientId AND {GetPatientReadPredicate()})";
		command.Parameters.AddWithValue("$PatientId", patientId);
		AddReadAccessParameters(command);
		return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
	}

	private static async Task<bool> SaveVisitTestResultsAsync(
		SqliteConnection connection,
		SqliteTransaction transaction,
		int patientId,
		int checkupId,
		IReadOnlyList<PatientTestResultForm> tests,
		string username,
		string now,
		int hospitalId,
		CancellationToken cancellationToken)
	{
		if (tests.Count > PatientCheckupForm.MaxTestsPerVisit)
			return false;

		await using SqliteCommand existingCommand = connection.CreateCommand();
		existingCommand.Transaction = transaction;
		existingCommand.CommandText =
			"""
			SELECT TestResultId FROM PatientTestResults
			WHERE PatientId = $PatientId AND CheckupId = $CheckupId AND HospitalId = $HospitalId
			""";
		existingCommand.Parameters.AddWithValue("$PatientId", patientId);
		existingCommand.Parameters.AddWithValue("$CheckupId", checkupId);
		existingCommand.Parameters.AddWithValue("$HospitalId", hospitalId);
		await using SqliteDataReader reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
		HashSet<int> existingIds = [];
		while (await reader.ReadAsync(cancellationToken))
			existingIds.Add(reader.GetInt32(0));
		await reader.DisposeAsync();

		int[] requestedIds = tests.Where(test => test.Id != 0).Select(test => test.Id).ToArray();
		if (requestedIds.Length != requestedIds.Distinct().Count() ||
			requestedIds.Any(id => !existingIds.Contains(id)))
		{
			return false;
		}

		foreach (int deletedId in existingIds.Except(requestedIds))
		{
			await using SqliteCommand deleteCommand = connection.CreateCommand();
			deleteCommand.Transaction = transaction;
			deleteCommand.CommandText =
				"""
				DELETE FROM PatientTestResults
				WHERE PatientId = $PatientId AND CheckupId = $CheckupId
					AND TestResultId = $TestResultId AND HospitalId = $HospitalId
				""";
			deleteCommand.Parameters.AddWithValue("$PatientId", patientId);
			deleteCommand.Parameters.AddWithValue("$CheckupId", checkupId);
			deleteCommand.Parameters.AddWithValue("$TestResultId", deletedId);
			deleteCommand.Parameters.AddWithValue("$HospitalId", hospitalId);
			await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		foreach (PatientTestResultForm test in tests)
		{
			await using SqliteCommand testCommand = connection.CreateCommand();
			testCommand.Transaction = transaction;
			if (test.Id == 0)
			{
				testCommand.CommandText =
					"""
					INSERT INTO PatientTestResults (
						PatientId, HospitalId, CheckupId, TestDate, TestName, Result, Unit, ReferenceRange, Notes,
						CreatedBy, CreatedAtUtc, UpdatedBy, UpdatedAtUtc)
					VALUES (
						$PatientId, $HospitalId, $CheckupId, $TestDate, $TestName, $Result, $Unit, $ReferenceRange, $Notes,
						$Username, $Now, $Username, $Now)
					""";
			}
			else
			{
				testCommand.CommandText =
					"""
					UPDATE PatientTestResults
					SET TestDate = $TestDate, TestName = $TestName, Result = $Result,
						Unit = $Unit, ReferenceRange = $ReferenceRange, Notes = $Notes,
						UpdatedBy = $Username, UpdatedAtUtc = $Now
					WHERE PatientId = $PatientId AND CheckupId = $CheckupId
						AND TestResultId = $TestResultId AND HospitalId = $HospitalId
					""";
				testCommand.Parameters.AddWithValue("$TestResultId", test.Id);
			}

			testCommand.Parameters.AddWithValue("$PatientId", patientId);
			testCommand.Parameters.AddWithValue("$HospitalId", hospitalId);
			testCommand.Parameters.AddWithValue("$CheckupId", checkupId);
			AddTestResultParameters(testCommand, test, username, now);
			if (await testCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
				return false;
		}

		return true;
	}

	private void AddCheckupParameters(
		SqliteCommand command,
		int patientId,
		PatientCheckupForm checkup,
		string username,
		string now)
	{
		command.Parameters.AddWithValue("$PatientId", patientId);
		command.Parameters.AddWithValue("$HospitalId", currentHospital.HospitalId);
		command.Parameters.AddWithValue("$CheckupDate", FormatDate(checkup.CheckupDate));
		command.Parameters.AddWithValue("$Reason", checkup.History.ChiefComplaint.Trim());
		command.Parameters.AddWithValue("$Notes", checkup.History.AdditionalNotes.Trim());
		command.Parameters.AddWithValue("$MedicalHistoryJson", JsonSerializer.Serialize(checkup.History));
		command.Parameters.Add("$FollowUpDate", SqliteType.Text).Value =
			checkup.FollowUpDate.HasValue ? FormatDate(checkup.FollowUpDate.Value) : DBNull.Value;
		command.Parameters.AddWithValue("$Username", username);
		command.Parameters.AddWithValue("$Now", now);
	}

	private static void AddTestResultParameters(
		SqliteCommand command,
		PatientTestResultForm result,
		string username,
		string now)
	{
		command.Parameters.AddWithValue("$TestDate", FormatDate(result.TestDate));
		command.Parameters.AddWithValue("$TestName", result.TestName.Trim());
		command.Parameters.AddWithValue("$Result", result.Result.Trim());
		command.Parameters.AddWithValue("$Unit", result.Unit.Trim());
		command.Parameters.AddWithValue("$ReferenceRange", result.ReferenceRange.Trim());
		command.Parameters.AddWithValue("$Notes", result.Notes.Trim());
		command.Parameters.AddWithValue("$Username", username);
		command.Parameters.AddWithValue("$Now", now);
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

	private static void AddPatientParameters(SqliteCommand command, PatientForm patient)
	{
		command.Parameters.AddWithValue("$FirstName", patient.FirstName.Trim());
		command.Parameters.AddWithValue("$LastName", patient.LastName.Trim());
		command.Parameters.AddWithValue("$Gender", patient.Gender.Trim());
		command.Parameters.AddWithValue("$DateOfBirth", FormatDate(patient.DateOfBirth));
		command.Parameters.AddWithValue("$Address", patient.FormatAddress());
		command.Parameters.AddWithValue(
			"$Phone",
			PhoneNumberSupport.NormalizeOrThrow(patient.Phone, patient.PhoneRegion));
		command.Parameters.AddWithValue("$EmergencyContactName", patient.EmergencyContactName?.Trim() ?? string.Empty);
		command.Parameters.AddWithValue("$SharedPatientId", patient.SharedPatientId.Trim().ToUpperInvariant());
		command.Parameters.AddWithValue(
			"$EmergencyContactPhone",
			string.IsNullOrWhiteSpace(patient.EmergencyContactPhone)
				? string.Empty
				: PhoneNumberSupport.NormalizeOrThrow(
					patient.EmergencyContactPhone,
					patient.EmergencyContactPhoneRegion));
		command.Parameters.AddWithValue("$AdmissionDate", FormatDate(patient.AdmissionDate));
		command.Parameters.Add("$DischargeDate", SqliteType.Text).Value =
			patient.DischargeDate.HasValue ? FormatDate(patient.DischargeDate.Value) : DBNull.Value;
		command.Parameters.AddWithValue("$Status", patient.Status);
	}

	private static PatientRecord ReadPatient(SqliteDataReader reader) =>
		new(
			reader.GetInt32(0),
			reader.GetInt32(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetString(4),
			DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
			reader.GetString(6),
			reader.GetString(7),
			ParseDate(reader.GetString(8)),
			reader.IsDBNull(9) ? null : ParseDate(reader.GetString(9)),
			reader.GetInt32(10),
			reader.GetString(11),
			reader.GetString(12),
			reader.GetString(13));

	private static string FormatDate(DateTime date) =>
		date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

	private static DateTime ParseDate(string date) =>
		DateTime.Parse(date, CultureInfo.InvariantCulture);

	private static DateTime ParseTimestamp(string timestamp) =>
		DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
