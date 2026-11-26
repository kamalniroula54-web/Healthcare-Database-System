namespace MK_Hospital.Data;

public interface IUserAccountService
{
	Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);
	Task<bool> CreateInitialAdminAsync(string username, string password, CancellationToken cancellationToken);
	Task<bool> VerifyCredentialsAsync(string username, string password, CancellationToken cancellationToken);
}
