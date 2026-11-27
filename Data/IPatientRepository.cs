namespace MK_Hospital.Data;

public interface IPatientRepository
{
	Task<IReadOnlyList<PatientSummary>> GetSummariesAsync(CancellationToken cancellationToken);
	Task<PatientSummary?> GetSummaryByIdAsync(int patientId, CancellationToken cancellationToken);
	Task<PatientRecord?> GetByIdAsync(int patientId, CancellationToken cancellationToken);
	Task<PatientDateUpdate?> GetDatesByIdAsync(int patientId, CancellationToken cancellationToken);
	Task CreateAsync(PatientForm patient, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(int patientId, PatientForm patient, CancellationToken cancellationToken);
	Task<bool> UpdateDatesAsync(
		int patientId,
		PatientDateUpdate dates,
		string changedBy,
		CancellationToken cancellationToken);
	Task<bool> DeleteAsync(int patientId, CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientDateChange>> GetUnacknowledgedDateChangesAsync(CancellationToken cancellationToken);
	Task<bool> AcknowledgeDateChangeAsync(int changeId, CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientCheckup>> GetCheckupsAsync(int patientId, CancellationToken cancellationToken);
	Task<bool> SaveCheckupAsync(
		int patientId,
		PatientCheckupForm checkup,
		string username,
		CancellationToken cancellationToken);
	Task<bool> DeleteCheckupAsync(int patientId, int checkupId, CancellationToken cancellationToken);
	Task<IReadOnlyList<PatientTestResult>> GetTestResultsAsync(int patientId, CancellationToken cancellationToken);
	Task<bool> SaveTestResultAsync(
		int patientId,
		PatientTestResultForm result,
		string username,
		CancellationToken cancellationToken);
	Task<bool> DeleteTestResultAsync(int patientId, int testResultId, CancellationToken cancellationToken);
}
