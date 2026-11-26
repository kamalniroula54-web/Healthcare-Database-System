using System.Globalization;
using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class SqlitePatientRepository(LocalDatabase database) : IPatientRepository
{
	private const string SelectColumns =
		"PatientId, FirstName, LastName, Gender, DateOfBirth, Address, Phone, AdmissionDate, DischargeDate, Status";

	public async Task<IReadOnlyList<PatientRecord>> GetAllAsync(CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"SELECT {SelectColumns} FROM Patients ORDER BY PatientId DESC";
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

		List<PatientRecord> patients = [];
		while (await reader.ReadAsync(cancellationToken))
			patients.Add(ReadPatient(reader));

		return patients;
	}

	public async Task<PatientRecord?> GetByIdAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"SELECT {SelectColumns} FROM Patients WHERE PatientId = $PatientId";
		command.Parameters.AddWithValue("$PatientId", patientId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
	}

	public async Task CreateAsync(PatientForm patient, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"INSERT INTO Patients (FirstName, LastName, Gender, DateOfBirth, Address, Phone, AdmissionDate, DischargeDate, Status) " +
			"VALUES ($FirstName, $LastName, $Gender, $DateOfBirth, $Address, $Phone, $AdmissionDate, $DischargeDate, $Status)";
		AddPatientParameters(command, patient);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<bool> UpdateAsync(int patientId, PatientForm patient, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"UPDATE Patients SET FirstName=$FirstName, LastName=$LastName, Gender=$Gender, DateOfBirth=$DateOfBirth, " +
			"Address=$Address, Phone=$Phone, AdmissionDate=$AdmissionDate, DischargeDate=$DischargeDate, Status=$Status " +
			"WHERE PatientId=$PatientId";
		AddPatientParameters(command, patient);
		command.Parameters.AddWithValue("$PatientId", patientId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	public async Task<bool> DeleteAsync(int patientId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "DELETE FROM Patients WHERE PatientId=$PatientId";
		command.Parameters.AddWithValue("$PatientId", patientId);
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static void AddPatientParameters(SqliteCommand command, PatientForm patient)
	{
		command.Parameters.AddWithValue("$FirstName", patient.FirstName.Trim());
		command.Parameters.AddWithValue("$LastName", patient.LastName.Trim());
		command.Parameters.AddWithValue("$Gender", patient.Gender.Trim());
		command.Parameters.AddWithValue("$DateOfBirth", FormatDate(patient.DateOfBirth));
		command.Parameters.AddWithValue("$Address", patient.Address.Trim());
		command.Parameters.AddWithValue("$Phone", patient.Phone.Trim());
		command.Parameters.AddWithValue("$AdmissionDate", FormatDate(patient.AdmissionDate));
		command.Parameters.Add("$DischargeDate", SqliteType.Text).Value =
			patient.DischargeDate.HasValue ? FormatDate(patient.DischargeDate.Value) : DBNull.Value;
		command.Parameters.AddWithValue("$Status", patient.Status);
	}

	private static PatientRecord ReadPatient(SqliteDataReader reader) =>
		new(
			reader.GetInt32(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetString(3),
			DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
			reader.GetString(5),
			reader.GetString(6),
			DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
			reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
			reader.GetInt32(9));

	private static string FormatDate(DateTime date) =>
		date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
