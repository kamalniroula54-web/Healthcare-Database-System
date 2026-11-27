using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Administrators)]
public sealed class NotificationsModel(IPatientRepository patients) : PageModel
{
	public IReadOnlyList<PatientDateChange> Changes { get; private set; } = [];

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Changes = await patients.GetUnacknowledgedDateChangesAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostAcknowledgeAsync(int id, CancellationToken cancellationToken)
	{
		bool acknowledged = await patients.AcknowledgeDateChangeAsync(id, cancellationToken);
		TempData["Message"] = acknowledged
			? "Notification acknowledged."
			: "That notification was already acknowledged or no longer exists.";
		return RedirectToPage();
	}
}
