namespace MK_Hospital.Data;

public interface IHospitalService
{
	Task<IReadOnlyList<HospitalSummary>> GetAllAsync(CancellationToken cancellationToken);
	Task<HospitalSummary?> GetByIdAsync(int hospitalId, CancellationToken cancellationToken);
	Task<HospitalDetails?> GetDetailsAsync(int hospitalId, CancellationToken cancellationToken);
	Task<HospitalLogo?> GetLogoAsync(int hospitalId, CancellationToken cancellationToken);
	Task<bool> ExistsAsync(int hospitalId, CancellationToken cancellationToken);
	Task<bool> CreateAsync(string name, CancellationToken cancellationToken);
	Task<bool> UpdateDetailsAsync(
		int hospitalId,
		HospitalProfileForm details,
		byte[]? logoData,
		string? logoContentType,
		CancellationToken cancellationToken);
}
