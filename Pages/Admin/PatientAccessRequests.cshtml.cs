using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Administrators)]
public sealed class PatientAccessRequestsModel(
	IPatientAccessService access,
	CurrentHospitalContext currentHospital) : PageModel
{
	public IReadOnlyList<PatientAccessRequestSummary> Requests { get; private set; } = [];
	public IReadOnlyList<PatientAccessGrantSummary> Grants { get; private set; } = [];
	public IReadOnlyDictionary<int, IReadOnlyList<PatientCandidate>> Candidates { get; private set; } =
		new Dictionary<int, IReadOnlyList<PatientCandidate>>();

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
	{
		if (!currentHospital.CanApprovePatientAccess)
			return Forbid();

		await LoadAsync(cancellationToken);
		return Page();
	}

	public async Task<IActionResult> OnPostApproveAsync(
		int requestId,
		int patientId,
		CancellationToken cancellationToken)
	{
		if (!currentHospital.CanApprovePatientAccess)
			return Forbid();

		if (!await access.ApproveRequestAsync(requestId, patientId, cancellationToken))
		{
			TempData["Message"] = "The request or selected patient is no longer available. Review the request and try again.";
			return RedirectToPage();
		}

		TempData["Message"] = "Patient access granted to the requesting hospital.";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostRejectAsync(int requestId, CancellationToken cancellationToken)
	{
		if (!currentHospital.CanApprovePatientAccess)
			return Forbid();

		TempData["Message"] = await access.RejectRequestAsync(requestId, cancellationToken)
			? "Patient access request rejected."
			: "That request was already resolved or no longer exists.";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostRevokeAsync(int grantId, CancellationToken cancellationToken)
	{
		if (!currentHospital.CanApprovePatientAccess)
			return Forbid();

		TempData["Message"] = await access.RevokeGrantAsync(grantId, cancellationToken)
			? "Patient access revoked."
			: "That grant was already revoked or no longer exists.";
		return RedirectToPage();
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		Requests = await access.GetAdminRequestsAsync(cancellationToken);
		Grants = await access.GetAdminGrantsAsync(cancellationToken);
		Dictionary<int, IReadOnlyList<PatientCandidate>> candidates = [];
		foreach (int hospitalId in Requests
			.Where(request => request.Status == "Pending")
			.Select(request => request.TargetHospitalId)
			.Distinct())
		{
			candidates[hospitalId] = await access.GetPatientCandidatesAsync(hospitalId, cancellationToken);
		}

		Candidates = candidates;
	}
}
