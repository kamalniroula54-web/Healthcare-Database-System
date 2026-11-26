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
		command.CommandText = "SELECT EXISTS(SELECT 1 FROM AdminUsers)";
		return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
	}

	public async Task<bool> CreateInitialAdminAsync(
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteTransaction transaction = connection.BeginTransaction();
		await using SqliteCommand existsCommand = connection.CreateCommand();
		existsCommand.Transaction = transaction;
		existsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM AdminUsers)";
		if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken)) != 0)
			return false;

		string passwordHash = passwordHasher.HashPassword(new LocalAdminUser(username, string.Empty), password);
		await using SqliteCommand insertCommand = connection.CreateCommand();
		insertCommand.Transaction = transaction;
		insertCommand.CommandText =
			"INSERT INTO AdminUsers (Id, Username, PasswordHash) VALUES (1, $Username, $PasswordHash)";
		insertCommand.Parameters.AddWithValue("$Username", username.Trim());
		insertCommand.Parameters.AddWithValue("$PasswordHash", passwordHash);
		await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<bool> VerifyCredentialsAsync(
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		await using SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken);
		await using SqliteCommand command = connection.CreateCommand();
		command.CommandText = "SELECT Username, PasswordHash FROM AdminUsers WHERE Username=$Username";
		command.Parameters.AddWithValue("$Username", username.Trim());
		await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
			return false;

		LocalAdminUser user = new(reader.GetString(0), reader.GetString(1));
		PasswordVerificationResult result =
			passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
		return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
	}
}
