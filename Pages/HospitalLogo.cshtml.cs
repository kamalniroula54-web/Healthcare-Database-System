using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

[Authorize(Roles = UserRoles.Staff)]
public sealed class HospitalLogoModel(
	IHospitalService hospitals,
	CurrentHospitalContext currentHospital) : PageModel
{
	public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
	{
		if (!currentHospital.IsAdministrator && id != currentHospital.HospitalId)
			return Forbid();

		HospitalLogo? logo = await hospitals.GetLogoAsync(id, cancellationToken);
		if (logo is null)
			return NotFound();

		Response.Headers["X-Content-Type-Options"] = "nosniff";
		Response.Headers["Cache-Control"] = "private, no-store";
		return File(logo.Content, logo.ContentType);
	}
}
