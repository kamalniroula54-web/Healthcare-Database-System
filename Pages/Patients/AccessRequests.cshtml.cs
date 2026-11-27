using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

[Authorize(Roles = UserRoles.Employee)]
public sealed class AccessRequestsModel(IPatientAccessService access, CurrentHospitalContext currentHospital) : PageModel
{
	[BindProperty]
	public PatientAccessRequestForm Input { get; set; } = new();

	public IReadOnlyList<HospitalSummary> Hospitals { get; private set; } = [];
	public IReadOnlyList<PatientAccessRequestSummary> Requests { get; private set; } = [];
	public IReadOnlyList<PatientAccessGrantSummary> Grants { get; private set; } = [];
	public int CurrentHospitalId => currentHospital.HospitalId;

	public async Task OnGetAsync(CancellationToken cancellationToken) =>
		await LoadAsync(cancellationToken);

	public async Task<IActionResult> OnPostRequestAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			await LoadAsync(cancellationToken);
			return Page();
		}

		bool created = await access.RequestAccessAsync(
			Input.TargetHospitalId,
			Input.PatientReference,
			Input.Reason,
			cancellationToken);
		if (!created)
		{
			ModelState.AddModelError(string.Empty, "The request could not be created. Check the hospital and whether a matching request is already pending.");
			await LoadAsync(cancellationToken);
			return Page();
		}

		TempData["Message"] = "Patient access request sent to the administrators.";
		return RedirectToPage();
	}

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		Hospitals = await access.GetRequestableHospitalsAsync(cancellationToken);
		Requests = await access.GetEmployeeRequestsAsync(cancellationToken);
		Grants = await access.GetEmployeeGrantsAsync(cancellationToken);
	}
}

public sealed class PatientAccessRequestForm
{
	[Range(1, int.MaxValue)]
	public int TargetHospitalId { get; set; }

	[Required, StringLength(200, MinimumLength = 1)]
	public string PatientReference { get; set; } = string.Empty;

	[Required, StringLength(1000, MinimumLength = 1)]
	public string Reason { get; set; } = string.Empty;
}
