using Microsoft.Data.Sqlite;

namespace MK_Hospital.Data;

public sealed class SqliteHospitalService(LocalDatabase database) : IHospitalService
{
	public async Task<IReadOnlyList<HospitalSummary>> GetAllAsync(CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT Id, Name, LogoData IS NOT NULL FROM Hospitals WHERE IsActive = 1 ORDER BY Name";
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		List<HospitalSummary> hospitals = [];
		while (await reader.ReadAsync(cancellationToken))
			hospitals.Add(new HospitalSummary(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetInt32(2) != 0));
		return hospitals;
	}

	public async Task<HospitalSummary?> GetByIdAsync(int hospitalId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"SELECT Id, Name, LogoData IS NOT NULL FROM Hospitals WHERE Id = $Id AND IsActive = 1";
		command.Parameters.AddWithValue("$Id", hospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new HospitalSummary(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2) != 0)
			: null;
	}

	public async Task<HospitalDetails?> GetDetailsAsync(
		int hospitalId,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT Id, Name, LicenseNumber, Address, ContactPhone, ContactEmail, LogoData IS NOT NULL
			FROM Hospitals
			WHERE Id = $Id AND IsActive = 1
			""";
		command.Parameters.AddWithValue("$Id", hospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new HospitalDetails(
				reader.GetInt32(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				reader.GetString(5),
				reader.GetInt32(6) != 0)
			: null;
	}

	public async Task<HospitalLogo?> GetLogoAsync(int hospitalId, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			SELECT LogoData, LogoContentType
			FROM Hospitals
			WHERE Id = $Id AND IsActive = 1 AND LogoData IS NOT NULL
				AND LogoContentType IN ('image/png', 'image/jpeg')
			""";
		command.Parameters.AddWithValue("$Id", hospitalId);
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken)
			? new HospitalLogo(reader.GetFieldValue<byte[]>(0), reader.GetString(1))
			: null;
	}

	public async Task<bool> ExistsAsync(int hospitalId, CancellationToken cancellationToken) =>
		await GetByIdAsync(hospitalId, cancellationToken) is not null;

	public async Task<bool> CreateAsync(string name, CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"INSERT INTO Hospitals (Name) VALUES ($Name) ON CONFLICT(Name) DO NOTHING";
		command.Parameters.AddWithValue("$Name", name.Trim());
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}

	public async Task<bool> UpdateDetailsAsync(
		int hospitalId,
		HospitalProfileForm details,
		byte[]? logoData,
		string? logoContentType,
		CancellationToken cancellationToken)
	{
		if (logoData is not null &&
			(!HospitalLogoValidator.TryGetContentType(logoData, out string? detectedContentType) ||
				logoContentType != detectedContentType))
		{
			return false;
		}

		if ((logoData is null) != (logoContentType is null))
			throw new ArgumentException("Logo data and content type must be supplied together.");

		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE Hospitals
			SET Name = $Name,
				LicenseNumber = $LicenseNumber,
				Address = $Address,
				ContactPhone = $ContactPhone,
				ContactEmail = $ContactEmail,
				LogoData = CASE
					WHEN $LogoData IS NOT NULL THEN $LogoData
					WHEN $RemoveLogo = 1 THEN NULL
					ELSE LogoData END,
				LogoContentType = CASE
					WHEN $LogoData IS NOT NULL THEN $LogoContentType
					WHEN $RemoveLogo = 1 THEN NULL
					ELSE LogoContentType END
				WHERE Id = $Id AND IsActive = 1
					AND NOT EXISTS (
						SELECT 1 FROM Hospitals otherHospital
						WHERE otherHospital.Name = $Name AND otherHospital.Id != $Id)
			""";
		command.Parameters.AddWithValue("$Id", hospitalId);
		command.Parameters.AddWithValue("$Name", details.Name.Trim());
		command.Parameters.AddWithValue("$LicenseNumber", details.LicenseNumber.Trim());
		command.Parameters.AddWithValue("$Address", details.Address.Trim());
		command.Parameters.AddWithValue("$ContactPhone", details.ContactPhone.Trim());
		command.Parameters.AddWithValue("$ContactEmail", details.ContactEmail.Trim());
		command.Parameters.Add("$LogoData", SqliteType.Blob).Value = (object?)logoData ?? DBNull.Value;
		command.Parameters.AddWithValue("$LogoContentType", (object?)logoContentType ?? DBNull.Value);
		command.Parameters.AddWithValue("$RemoveLogo", details.RemoveLogo ? 1 : 0);
		return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
	}
}
