namespace MK_Hospital.Data;

public interface IUserAccountService
{
	Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);
	Task<AccountAuthenticationState?> GetAuthenticationStateAsync(
		string username,
		CancellationToken cancellationToken);
	Task<bool> CreateInitialAdminAsync(
		string username,
		string password,
		EmployeeProfile profile,
		CancellationToken cancellationToken);
	Task<UserAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
	Task<string?> CreateEmployeeAsync(string username, int hospitalId, CancellationToken cancellationToken);
	Task<string?> ResetEmployeePasswordAsync(
		string username,
		int? hospitalId,
		bool includeHospitalAdmins,
		CancellationToken cancellationToken);
	Task<IReadOnlyList<EmployeeAccountSummary>> GetEmployeesAsync(
		int? hospitalId,
		CancellationToken cancellationToken);
	Task<EmployeeAccountSummary?> GetEmployeeAsync(
		string username,
		int? hospitalId,
		CancellationToken cancellationToken);
	Task<string?> CreateAdministratorAsync(string username, CancellationToken cancellationToken);
	Task<string?> CreateHospitalAdministratorAsync(
		string username,
		int hospitalId,
		bool canApprovePatientAccess,
		CancellationToken cancellationToken);
	Task<IReadOnlyList<EmployeeAccountSummary>> GetHospitalAdministratorsAsync(
		CancellationToken cancellationToken);
	Task<bool> UpdateHospitalAdministratorAsync(
		string username,
		int hospitalId,
		bool canApprovePatientAccess,
		CancellationToken cancellationToken);
	Task<bool> RemoveHospitalAdministratorAsync(string username, CancellationToken cancellationToken);
	Task<string?> ResetAdministratorPasswordAsync(
		string username,
		string requestingAdminUsername,
		CancellationToken cancellationToken);
	Task<IReadOnlyList<EmployeeAccountSummary>> GetAdministratorsAsync(CancellationToken cancellationToken);
	Task RequestPhonePasswordResetAsync(
		string username,
		CancellationToken cancellationToken);
	Task<EmailPasswordReset?> PrepareEmailPasswordResetAsync(
		string username,
		CancellationToken cancellationToken);
	Task<bool> CompleteEmailPasswordResetAsync(
		int requestId,
		string temporaryPassword,
		CancellationToken cancellationToken);
	Task MarkPasswordResetEmailFailedAsync(
		int requestId,
		string errorCode,
		CancellationToken cancellationToken);
	Task<IReadOnlyList<PasswordResetRequest>> GetPendingPasswordResetRequestsAsync(
		CancellationToken cancellationToken);
	Task<EmployeePasswordReset?> IssueRequestedPasswordResetAsync(
		int requestId,
		string adminUsername,
		CancellationToken cancellationToken);
	Task<EmployeeProfile?> GetProfileAsync(string username, CancellationToken cancellationToken);
	Task<EmployeeProfile?> GetEmployeeProfileAsync(string username, CancellationToken cancellationToken);
	Task<bool> UpdateProfileAsync(
		string username,
		EmployeeProfile profile,
		CancellationToken cancellationToken);
	Task<bool> UpdateEmployeeProfileAsync(
		string username,
		EmployeeProfile profile,
		int hospitalId,
		int? expectedHospitalId,
		CancellationToken cancellationToken);
	Task<bool> CompleteAccountSetupAsync(
		string username,
		EmployeeProfile profile,
		string? newPassword,
		CancellationToken cancellationToken);
}
