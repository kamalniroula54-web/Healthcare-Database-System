namespace MK_Hospital.Data;

public interface IPatientRepository
{
	Task<IReadOnlyList<PatientRecord>> GetAllAsync(CancellationToken cancellationToken);
	Task<PatientRecord?> GetByIdAsync(int patientId, CancellationToken cancellationToken);
	Task CreateAsync(PatientForm patient, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(int patientId, PatientForm patient, CancellationToken cancellationToken);
	Task<bool> DeleteAsync(int patientId, CancellationToken cancellationToken);
}
