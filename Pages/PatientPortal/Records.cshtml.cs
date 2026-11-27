using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.PatientPortal;

[Authorize(Roles = UserRoles.Patient)]
public sealed class RecordsModel(IPatientPortalService patientPortal) : PageModel
{
	public IReadOnlyList<PatientPortalRecord> Records { get; private set; } = [];

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
	{
		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		Records = await patientPortal.GetRecordsAsync(username, cancellationToken);
		return Page();
	}
}
