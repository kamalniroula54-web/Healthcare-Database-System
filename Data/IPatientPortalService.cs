namespace MK_Hospital.Data;

public interface IPatientPortalService
{
	Task<PatientPortalUser?> AuthenticateAsync(
		string username,
		string password,
		CancellationToken cancellationToken);

	Task<PatientPortalAuthenticationState?> GetAuthenticationStateAsync(
		string username,
		CancellationToken cancellationToken);

	Task<string?> CreateRegistrationCodeAsync(
		int patientId,
		string createdBy,
		CancellationToken cancellationToken);

	Task<PatientPortalRegistrationStatus> RegisterAsync(
		string registrationCode,
		string username,
		string password,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<PatientPortalRecord>> GetRecordsAsync(
		string username,
		CancellationToken cancellationToken);
}
