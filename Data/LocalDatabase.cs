using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class LocalDatabase
{
	private readonly string connectionString;

	public LocalDatabase()
	{
		string dataDirectory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"MKHospital");
		Directory.CreateDirectory(dataDirectory);

		connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = Path.Combine(dataDirectory, "patients.db"),
			Mode = SqliteOpenMode.ReadWriteCreate,
			Cache = SqliteCacheMode.Shared
		}.ToString();
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			CREATE TABLE IF NOT EXISTS Patients (
				PatientId INTEGER PRIMARY KEY AUTOINCREMENT,
				FirstName TEXT NOT NULL,
				LastName TEXT NOT NULL,
				Gender TEXT NOT NULL,
				DateOfBirth TEXT NOT NULL,
				Address TEXT NOT NULL,
				Phone TEXT NOT NULL,
				AdmissionDate TEXT NOT NULL,
				DischargeDate TEXT NULL,
				Status INTEGER NOT NULL CHECK (Status IN (1, 2))
			);

			CREATE TABLE IF NOT EXISTS AdminUsers (
				Id INTEGER PRIMARY KEY CHECK (Id = 1),
				Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
				PasswordHash TEXT NOT NULL
			);
			""";
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
	{
		SqliteConnection connection = new(connectionString);
		await connection.OpenAsync(cancellationToken);
		return connection;
	}
}
