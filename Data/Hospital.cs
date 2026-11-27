using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MK_Hospital.Data;

public sealed record HospitalSummary(int HospitalId, string Name, bool HasLogo = false);

public sealed record HospitalDetails(
	int HospitalId,
	string Name,
	string LicenseNumber,
	string Address,
	string ContactPhone,
	string ContactEmail,
	bool HasLogo);

public sealed record HospitalLogo(byte[] Content, string ContentType);

public static class HospitalClaims
{
	public const string HospitalId = "hospital_id";
}

public sealed class CurrentHospitalContext(IHttpContextAccessor httpContextAccessor)
{
	private ClaimsPrincipal Principal =>
		httpContextAccessor.HttpContext?.User
		?? throw new InvalidOperationException("The current request has no authenticated user.");

	public int HospitalId =>
		int.TryParse(Principal.FindFirstValue(HospitalClaims.HospitalId), out int hospitalId)
			? hospitalId
			: throw new InvalidOperationException("The authenticated user has no hospital assignment.");

	public string Username =>
		Principal.Identity?.Name
		?? throw new InvalidOperationException("The authenticated user has no username.");

	public bool IsAdministrator => Principal.IsInRole(UserRoles.Admin);

	public bool IsHospitalAdministrator => Principal.IsInRole(UserRoles.HospitalAdmin);

	public bool CanApprovePatientAccess =>
		IsAdministrator ||
		(IsHospitalAdministrator && Principal.HasClaim(UserAccountClaims.CanApprovePatientAccess, "true"));

	public bool IsEmployee => Principal.IsInRole(UserRoles.Employee);
}
