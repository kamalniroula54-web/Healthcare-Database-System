namespace MK_Hospital.Data;

public interface IPatientAccessService
{
	Task<IReadOnlyList<HospitalSummary>> GetRequestableHospitalsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientAccessRequestSummary>> GetEmployeeRequestsAsync(CancellationToken cancellationToken);
	Task<bool> RequestAccessAsync(
		int targetHospitalId,
		string patientReference,
		string reason,
		CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientAccessRequestSummary>> GetAdminRequestsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientCandidate>> GetPatientCandidatesAsync(
		int hospitalId,
		CancellationToken cancellationToken);
	Task<bool> ApproveRequestAsync(int requestId, int patientId, CancellationToken cancellationToken);
	Task<bool> RejectRequestAsync(int requestId, CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientAccessGrantSummary>> GetEmployeeGrantsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientAccessGrantSummary>> GetAdminGrantsAsync(CancellationToken cancellationToken);
	Task<bool> RevokeGrantAsync(int grantId, CancellationToken cancellationToken);
}
