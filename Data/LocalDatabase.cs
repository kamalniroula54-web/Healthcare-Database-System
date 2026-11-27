using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class LocalDatabase
{
	private readonly string connectionString;

	public string DataDirectory { get; }

	public LocalDatabase()
	{
		string? configuredDirectory = Environment.GetEnvironmentVariable("MK_HOSPITAL_DATA_DIRECTORY");
		DataDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKHospital")
			: Path.GetFullPath(configuredDirectory);
		Directory.CreateDirectory(DataDirectory);

		connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = Path.Combine(DataDirectory, "patients.db"),
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared,
			ForeignKeys = true
		}.ToString();
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS Hospitals (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
				LicenseNumber TEXT NOT NULL DEFAULT '',
				Address TEXT NOT NULL DEFAULT '',
				ContactPhone TEXT NOT NULL DEFAULT '',
				ContactEmail TEXT NOT NULL DEFAULT '',
				LogoData BLOB NULL,
				LogoContentType TEXT NULL,
				IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1))
			);

			INSERT OR IGNORE INTO Hospitals (Id, Name) VALUES (1, 'MK Hospital');

			CREATE TABLE IF NOT EXISTS Patients (
				PatientId INTEGER PRIMARY KEY AUTOINCREMENT,
				HospitalId INTEGER NOT NULL DEFAULT 1,
				FirstName TEXT NOT NULL,
				LastName TEXT NOT NULL,
				Gender TEXT NOT NULL,
				DateOfBirth TEXT NOT NULL,
				Address TEXT NOT NULL,
				Phone TEXT NOT NULL,
				EmergencyContactName TEXT NOT NULL DEFAULT '',
				EmergencyContactPhone TEXT NOT NULL DEFAULT '',
				SharedPatientId TEXT NOT NULL DEFAULT '',
				AdmissionDate TEXT NOT NULL,
				DischargeDate TEXT NULL,
				Status INTEGER NOT NULL CHECK (Status IN (1, 2))
			);

			CREATE TABLE IF NOT EXISTS PatientPortalAccounts (
				Username TEXT PRIMARY KEY COLLATE NOCASE,
				PasswordHash TEXT NOT NULL,
				CreatedAtUtc TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS PatientPortalPatientLinks (
				PatientId INTEGER PRIMARY KEY REFERENCES Patients(PatientId) ON DELETE CASCADE,
				Username TEXT NOT NULL COLLATE NOCASE
					REFERENCES PatientPortalAccounts(Username) ON DELETE CASCADE,
				LinkedAtUtc TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientPortalPatientLinks_Username
				ON PatientPortalPatientLinks (Username, PatientId);

			CREATE TABLE IF NOT EXISTS PatientPortalIdentityLinks (
				SharedPatientId TEXT PRIMARY KEY,
				Username TEXT NOT NULL COLLATE NOCASE
					REFERENCES PatientPortalAccounts(Username) ON DELETE CASCADE,
				LinkedAtUtc TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientPortalIdentityLinks_Username
				ON PatientPortalIdentityLinks (Username, SharedPatientId);

			CREATE TABLE IF NOT EXISTS PatientPortalRegistrationCodes (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				PatientId INTEGER NOT NULL REFERENCES Patients(PatientId) ON DELETE CASCADE,
				CodeHash TEXT NOT NULL UNIQUE,
				CreatedBy TEXT NOT NULL,
				CreatedAtUtc TEXT NOT NULL,
				ExpiresAtUtc TEXT NOT NULL,
				RedeemedAtUtc TEXT NULL,
				RedeemedBy TEXT NULL,
				RevokedAtUtc TEXT NULL
			);

			CREATE UNIQUE INDEX IF NOT EXISTS IX_PatientPortalRegistrationCodes_ActivePatient
				ON PatientPortalRegistrationCodes (PatientId)
				WHERE RedeemedAtUtc IS NULL AND RevokedAtUtc IS NULL;

			CREATE TABLE IF NOT EXISTS AdminUsers (
				Id INTEGER PRIMARY KEY CHECK (Id = 1),
				Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
				PasswordHash TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS UserAccounts (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
				PasswordHash TEXT NOT NULL,
				Role TEXT NOT NULL CHECK (Role IN ('Admin', 'Employee')),
				HospitalId INTEGER NOT NULL DEFAULT 1,
				MustChangePassword INTEGER NOT NULL DEFAULT 0,
				FirstName TEXT NOT NULL DEFAULT '',
				LastName TEXT NOT NULL DEFAULT '',
				DateOfBirth TEXT NULL,
				Email TEXT NOT NULL DEFAULT '',
				Phone TEXT NOT NULL DEFAULT '',
				PasswordResetEmailAttemptedAtUtc TEXT NULL,
				EmergencyContactName TEXT NOT NULL DEFAULT '',
				EmergencyContactPhone TEXT NOT NULL DEFAULT '',
				EmergencyContactRelationship TEXT NOT NULL DEFAULT '',
				ClinicalRole TEXT NOT NULL DEFAULT '',
				IsHospitalAdmin INTEGER NOT NULL DEFAULT 0,
				CanApprovePatientAccess INTEGER NOT NULL DEFAULT 0,
				ProfileComplete INTEGER NOT NULL DEFAULT 0
			);

			CREATE TABLE IF NOT EXISTS PatientAccessRequests (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				EmployeeUsername TEXT NOT NULL,
				EmployeeHospitalId INTEGER NOT NULL REFERENCES Hospitals(Id),
				TargetHospitalId INTEGER NOT NULL REFERENCES Hospitals(Id),
				PatientReference TEXT NOT NULL,
				Reason TEXT NOT NULL,
				RequestedAtUtc TEXT NOT NULL,
				Status TEXT NOT NULL DEFAULT 'Pending'
					CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
				ResolvedBy TEXT NULL,
				ResolvedAtUtc TEXT NULL,
				GrantedPatientId INTEGER NULL REFERENCES Patients(PatientId) ON DELETE SET NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientAccessRequests_Employee
				ON PatientAccessRequests (EmployeeUsername, RequestedAtUtc DESC);

			CREATE INDEX IF NOT EXISTS IX_PatientAccessRequests_Status
				ON PatientAccessRequests (Status, RequestedAtUtc DESC);

			CREATE INDEX IF NOT EXISTS IX_PatientAccessRequests_HospitalPatient
				ON PatientAccessRequests (EmployeeHospitalId, TargetHospitalId, GrantedPatientId);

			CREATE UNIQUE INDEX IF NOT EXISTS IX_PatientAccessRequests_PendingEmployeePatient
				ON PatientAccessRequests (
					EmployeeUsername COLLATE NOCASE,
					TargetHospitalId,
					PatientReference COLLATE NOCASE)
				WHERE Status = 'Pending';

			CREATE TABLE IF NOT EXISTS PatientAccessGrants (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				RequestId INTEGER NOT NULL UNIQUE REFERENCES PatientAccessRequests(Id),
				EmployeeUsername TEXT NOT NULL,
				TargetHospitalId INTEGER NOT NULL REFERENCES Hospitals(Id),
				PatientId INTEGER NOT NULL REFERENCES Patients(PatientId) ON DELETE CASCADE,
				GrantedBy TEXT NOT NULL,
				GrantedAtUtc TEXT NOT NULL,
				RevokedBy TEXT NULL,
				RevokedAtUtc TEXT NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientAccessGrants_EmployeePatient
				ON PatientAccessGrants (EmployeeUsername, TargetHospitalId, PatientId, RevokedAtUtc);

			CREATE INDEX IF NOT EXISTS IX_PatientAccessGrants_PatientTarget
				ON PatientAccessGrants (PatientId, TargetHospitalId, RevokedAtUtc, RequestId);

			CREATE TABLE IF NOT EXISTS PatientDateChanges (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				PatientId INTEGER NOT NULL,
				ChangedBy TEXT NOT NULL,
				PreviousAdmissionDate TEXT NOT NULL,
				NewAdmissionDate TEXT NOT NULL,
				PreviousDischargeDate TEXT NULL,
				NewDischargeDate TEXT NULL,
				ChangedAtUtc TEXT NOT NULL,
				IsAcknowledged INTEGER NOT NULL DEFAULT 0
			);

			CREATE INDEX IF NOT EXISTS IX_PatientDateChanges_IsAcknowledged
				ON PatientDateChanges (IsAcknowledged, ChangedAtUtc DESC);

			CREATE TABLE IF NOT EXISTS PatientCheckups (
				CheckupId INTEGER PRIMARY KEY AUTOINCREMENT,
				PatientId INTEGER NOT NULL REFERENCES Patients(PatientId) ON DELETE CASCADE,
				HospitalId INTEGER NOT NULL DEFAULT 1,
				CheckupDate TEXT NOT NULL,
				Reason TEXT NOT NULL,
				Notes TEXT NOT NULL,
				MedicalHistoryJson TEXT NOT NULL DEFAULT '{}',
				FollowUpDate TEXT NULL,
				CreatedBy TEXT NOT NULL,
				CreatedAtUtc TEXT NOT NULL,
				UpdatedBy TEXT NOT NULL,
				UpdatedAtUtc TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientCheckups_PatientDate
				ON PatientCheckups (PatientId, CheckupDate DESC, CheckupId DESC);

			CREATE TABLE IF NOT EXISTS PatientTestResults (
				TestResultId INTEGER PRIMARY KEY AUTOINCREMENT,
				PatientId INTEGER NOT NULL REFERENCES Patients(PatientId) ON DELETE CASCADE,
				HospitalId INTEGER NOT NULL DEFAULT 1,
				CheckupId INTEGER NULL REFERENCES PatientCheckups(CheckupId) ON DELETE CASCADE,
				TestDate TEXT NOT NULL,
				TestName TEXT NOT NULL,
				Result TEXT NOT NULL,
				Unit TEXT NOT NULL DEFAULT '',
				ReferenceRange TEXT NOT NULL DEFAULT '',
				Notes TEXT NOT NULL DEFAULT '',
				CreatedBy TEXT NOT NULL,
				CreatedAtUtc TEXT NOT NULL,
				UpdatedBy TEXT NOT NULL,
				UpdatedAtUtc TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS IX_PatientTestResults_PatientDate
				ON PatientTestResults (PatientId, TestDate DESC, TestResultId DESC);

			CREATE TABLE IF NOT EXISTS PasswordResetRequests (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				Username TEXT NOT NULL COLLATE NOCASE,
				AccountRole TEXT NOT NULL DEFAULT 'Employee' CHECK (AccountRole IN ('Admin', 'Employee')),
				ContactMethod TEXT NOT NULL CHECK (ContactMethod IN ('Email', 'Phone')),
				RequestedAtUtc TEXT NOT NULL,
				DeliveryStatus TEXT NOT NULL DEFAULT 'ManualPending'
					CHECK (DeliveryStatus IN ('ManualPending', 'Sending', 'Sent', 'Failed')),
				DeliveryError TEXT NULL,
				ResolvedAtUtc TEXT NULL,
				ResolvedBy TEXT NULL
			);

			CREATE UNIQUE INDEX IF NOT EXISTS IX_PasswordResetRequests_PendingUsername
				ON PasswordResetRequests (Username)
				WHERE ResolvedAtUtc IS NULL;

			CREATE INDEX IF NOT EXISTS IX_PasswordResetRequests_PendingDate
				ON PasswordResetRequests (ResolvedAtUtc, RequestedAtUtc DESC);
			""";
		await command.ExecuteNonQueryAsync(cancellationToken);

		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"LicenseNumber",
			"ALTER TABLE Hospitals ADD COLUMN LicenseNumber TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"Address",
			"ALTER TABLE Hospitals ADD COLUMN Address TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"ContactPhone",
			"ALTER TABLE Hospitals ADD COLUMN ContactPhone TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"ContactEmail",
			"ALTER TABLE Hospitals ADD COLUMN ContactEmail TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"LogoData",
			"ALTER TABLE Hospitals ADD COLUMN LogoData BLOB NULL",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Hospitals",
			"LogoContentType",
			"ALTER TABLE Hospitals ADD COLUMN LogoContentType TEXT NULL",
			cancellationToken);

		await AddColumnIfMissingAsync(
			connection,
			"Patients",
			"HospitalId",
			"ALTER TABLE Patients ADD COLUMN HospitalId INTEGER NOT NULL DEFAULT 1",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Patients",
			"SharedPatientId",
			"ALTER TABLE Patients ADD COLUMN SharedPatientId TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await using (SqliteCommand patientIdentityCommand = connection.CreateCommand())
		{
			patientIdentityCommand.CommandText =
				"""
				UPDATE Patients
				SET SharedPatientId = UPPER(HEX(RANDOMBLOB(16)))
				WHERE SharedPatientId IS NULL OR trim(SharedPatientId) = '';
				INSERT OR IGNORE INTO PatientPortalIdentityLinks (SharedPatientId, Username, LinkedAtUtc)
				SELECT patient.SharedPatientId, link.Username, MIN(link.LinkedAtUtc)
				FROM PatientPortalPatientLinks link
				JOIN Patients patient ON patient.PatientId = link.PatientId
				GROUP BY patient.SharedPatientId, link.Username;
				CREATE INDEX IF NOT EXISTS IX_Patients_SharedPatientId
					ON Patients (SharedPatientId, PatientId);
				""";
			await patientIdentityCommand.ExecuteNonQueryAsync(cancellationToken);
		}
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"HospitalId",
			"ALTER TABLE UserAccounts ADD COLUMN HospitalId INTEGER NOT NULL DEFAULT 1",
			cancellationToken);
		await using (SqliteCommand hospitalIndexesCommand = connection.CreateCommand())
		{
			hospitalIndexesCommand.CommandText =
				"""
				CREATE INDEX IF NOT EXISTS IX_Patients_HospitalId
					ON Patients (HospitalId, PatientId DESC);
				CREATE INDEX IF NOT EXISTS IX_UserAccounts_HospitalId
					ON UserAccounts (HospitalId, Role);
				""";
			await hospitalIndexesCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await AddColumnIfMissingAsync(
			connection,
			"Patients",
			"EmergencyContactName",
			"ALTER TABLE Patients ADD COLUMN EmergencyContactName TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"Patients",
			"EmergencyContactPhone",
			"ALTER TABLE Patients ADD COLUMN EmergencyContactPhone TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"PatientCheckups",
			"MedicalHistoryJson",
			"ALTER TABLE PatientCheckups ADD COLUMN MedicalHistoryJson TEXT NOT NULL DEFAULT '{}'",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"PatientTestResults",
			"CheckupId",
			"ALTER TABLE PatientTestResults ADD COLUMN CheckupId INTEGER NULL REFERENCES PatientCheckups(CheckupId) ON DELETE CASCADE",
			cancellationToken);
		bool checkupHospitalIdAdded = await AddColumnIfMissingAsync(
			connection,
			"PatientCheckups",
			"HospitalId",
			"ALTER TABLE PatientCheckups ADD COLUMN HospitalId INTEGER NOT NULL DEFAULT 1",
			cancellationToken);
		if (checkupHospitalIdAdded)
		{
			await using SqliteCommand migrateCheckupHospitalsCommand = connection.CreateCommand();
			migrateCheckupHospitalsCommand.CommandText =
				"""
				UPDATE PatientCheckups
				SET HospitalId = (
					SELECT HospitalId FROM Patients
					WHERE Patients.PatientId = PatientCheckups.PatientId)
				""";
			await migrateCheckupHospitalsCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		bool testResultHospitalIdAdded = await AddColumnIfMissingAsync(
			connection,
			"PatientTestResults",
			"HospitalId",
			"ALTER TABLE PatientTestResults ADD COLUMN HospitalId INTEGER NOT NULL DEFAULT 1",
			cancellationToken);
		if (testResultHospitalIdAdded)
		{
			await using SqliteCommand migrateTestResultHospitalsCommand = connection.CreateCommand();
			migrateTestResultHospitalsCommand.CommandText =
				"""
				UPDATE PatientTestResults
				SET HospitalId = (
					SELECT HospitalId FROM Patients
					WHERE Patients.PatientId = PatientTestResults.PatientId)
				""";
			await migrateTestResultHospitalsCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await using (SqliteCommand checkupResultsIndexCommand = connection.CreateCommand())
		{
			checkupResultsIndexCommand.CommandText =
				"""
				CREATE INDEX IF NOT EXISTS IX_PatientTestResults_Checkup
					ON PatientTestResults (PatientId, CheckupId, TestDate DESC, TestResultId DESC);
				CREATE INDEX IF NOT EXISTS IX_PatientCheckups_HospitalDate
					ON PatientCheckups (HospitalId, PatientId, CheckupDate DESC, CheckupId DESC);
				CREATE INDEX IF NOT EXISTS IX_PatientTestResults_HospitalDate
					ON PatientTestResults (HospitalId, PatientId, TestDate DESC, TestResultId DESC)
				""";
			await checkupResultsIndexCommand.ExecuteNonQueryAsync(cancellationToken);
		}
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"FirstName",
			"ALTER TABLE UserAccounts ADD COLUMN FirstName TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"LastName",
			"ALTER TABLE UserAccounts ADD COLUMN LastName TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"DateOfBirth",
			"ALTER TABLE UserAccounts ADD COLUMN DateOfBirth TEXT NULL",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"Email",
			"ALTER TABLE UserAccounts ADD COLUMN Email TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"Phone",
			"ALTER TABLE UserAccounts ADD COLUMN Phone TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"EmergencyContactName",
			"ALTER TABLE UserAccounts ADD COLUMN EmergencyContactName TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"EmergencyContactPhone",
			"ALTER TABLE UserAccounts ADD COLUMN EmergencyContactPhone TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"EmergencyContactRelationship",
			"ALTER TABLE UserAccounts ADD COLUMN EmergencyContactRelationship TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"ClinicalRole",
			"ALTER TABLE UserAccounts ADD COLUMN ClinicalRole TEXT NOT NULL DEFAULT ''",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"IsHospitalAdmin",
			"ALTER TABLE UserAccounts ADD COLUMN IsHospitalAdmin INTEGER NOT NULL DEFAULT 0",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"CanApprovePatientAccess",
			"ALTER TABLE UserAccounts ADD COLUMN CanApprovePatientAccess INTEGER NOT NULL DEFAULT 0",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"ProfileComplete",
			"ALTER TABLE UserAccounts ADD COLUMN ProfileComplete INTEGER NOT NULL DEFAULT 0",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"UserAccounts",
			"PasswordResetEmailAttemptedAtUtc",
			"ALTER TABLE UserAccounts ADD COLUMN PasswordResetEmailAttemptedAtUtc TEXT NULL",
			cancellationToken);

		await using (SqliteCommand employeeProfileCommand = connection.CreateCommand())
		{
			employeeProfileCommand.CommandText =
				"""
				UPDATE UserAccounts
				SET ProfileComplete = 0
				WHERE Role = 'Employee' AND ClinicalRole = '';
				""";
			await employeeProfileCommand.ExecuteNonQueryAsync(cancellationToken);
		}
		await AddColumnIfMissingAsync(
			connection,
			"PasswordResetRequests",
			"AccountRole",
			"ALTER TABLE PasswordResetRequests ADD COLUMN AccountRole TEXT NOT NULL DEFAULT 'Employee'",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"PasswordResetRequests",
			"DeliveryStatus",
			"ALTER TABLE PasswordResetRequests ADD COLUMN DeliveryStatus TEXT NOT NULL DEFAULT 'ManualPending'",
			cancellationToken);
		await AddColumnIfMissingAsync(
			connection,
			"PasswordResetRequests",
			"DeliveryError",
			"ALTER TABLE PasswordResetRequests ADD COLUMN DeliveryError TEXT NULL",
			cancellationToken);

		await using SqliteCommand migrateAdminCommand = connection.CreateCommand();
		migrateAdminCommand.CommandText =
			"""
			INSERT OR IGNORE INTO UserAccounts (
				Id, Username, PasswordHash, Role, MustChangePassword, ProfileComplete)
			SELECT Id, Username, PasswordHash, 'Admin', 0, 0 FROM AdminUsers;
			""";
		await migrateAdminCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> AddColumnIfMissingAsync(
		SqliteConnection connection,
		string table,
		string column,
		string alterSql,
		CancellationToken cancellationToken)
	{
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"PRAGMA table_info({table})";
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
				return false;
		}

		await reader.DisposeAsync();
		command.CommandText = alterSql;
		await command.ExecuteNonQueryAsync(cancellationToken);
		return true;
	}

	public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
	{
		SqliteConnection connection = new(connectionString);
		await connection.OpenAsync(cancellationToken);
		return connection;
	}
}
