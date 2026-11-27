using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

[Authorize(Roles = UserRoles.Staff)]
public sealed class IndexModel(IPatientRepository patients, CurrentHospitalContext currentHospital) : PageModel
{
	public IReadOnlyList<PatientSummary> Patients { get; private set; } = [];
	public IReadOnlyList<PatientDateChange> PendingDateChanges { get; private set; } = [];
	public int CurrentHospitalId => currentHospital.HospitalId;
	public bool CanManageLocalPatients =>
		User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.HospitalAdmin);

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Patients = await patients.GetSummariesAsync(cancellationToken);
		if (CanManageLocalPatients)
			PendingDateChanges = await patients.GetUnacknowledgedDateChangesAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
	{
		if (!CanManageLocalPatients)
			return Forbid();

		bool deleted = await patients.DeleteAsync(id, cancellationToken);
		TempData["Message"] = deleted ? "Patient deleted." : "That patient record was not found.";
		return RedirectToPage();
	}
}
