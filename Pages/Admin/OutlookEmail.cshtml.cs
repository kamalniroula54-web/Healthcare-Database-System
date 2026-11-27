using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public sealed class OutlookEmailModel(IOutlookMailSender mailSender) : PageModel
{
	public OutlookConnectionStatus Status { get; private set; } =
		new(false, false, null, null, null, null, "ClientIdMissing");

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Status = await mailSender.GetConnectionStatusAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostConnectAsync(CancellationToken cancellationToken)
	{
		Status = await mailSender.StartConnectionAsync(cancellationToken);
		if (!Status.IsConfigured)
			TempData["Message"] = "Outlook is not configured. Set Outlook__ClientId and restart the app.";
		else if (Status.IsConnected)
			TempData["Message"] = $"Outlook is connected as {Status.ConnectedEmail}.";

		return Page();
	}

	public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken)
	{
		await mailSender.DisconnectAsync(cancellationToken);
		TempData["Message"] = "Outlook has been disconnected.";
		return RedirectToPage();
	}
}
