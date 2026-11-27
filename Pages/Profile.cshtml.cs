using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

[Authorize(Roles = UserRoles.Staff)]
public sealed class ProfileModel(IUserAccountService accounts) : PageModel
{
	[BindProperty]
	public EmployeeProfileForm Input { get; set; } = new();

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
	{
		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		EmployeeProfile? profile = await accounts.GetProfileAsync(username, cancellationToken);
		if (profile is null)
			return NotFound();

		Input = EmployeeProfileForm.FromProfile(profile);
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (Input.DateOfBirth.HasValue && Input.DateOfBirth.Value.Date > DateTime.Today)
			ModelState.AddModelError("Input.DateOfBirth", "Date of birth cannot be in the future.");
		if (User.IsInRole(UserRoles.Employee) && string.IsNullOrWhiteSpace(Input.ClinicalRole))
			ModelState.AddModelError("Input.ClinicalRole", "Select your clinical position.");
		if (!ModelState.IsValid)
			return Page();

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		if (!await accounts.UpdateProfileAsync(username, Input.ToProfile(), cancellationToken))
			return NotFound();

		TempData["Message"] = "Profile updated.";
		return RedirectToPage();
	}
}
