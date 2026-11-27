using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

[Authorize(Roles = UserRoles.Staff)]
public sealed class ChangePasswordModel(IUserAccountService accounts) : PageModel
{
	[BindProperty]
	public EmployeeProfileForm Profile { get; set; } = new();

	[BindProperty]
	public string? Password { get; set; }

	[BindProperty]
	public string? ConfirmPassword { get; set; }

	public bool RequiresPasswordChange =>
		User.HasClaim(UserAccountClaims.MustChangePassword, "true");

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
	{
		if (!NeedsAccountSetup())
			return RedirectToPage("/Index");

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		EmployeeProfile? profile = await accounts.GetProfileAsync(username, cancellationToken);
		if (profile is null)
			return NotFound();

		Profile = EmployeeProfileForm.FromProfile(profile);
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!NeedsAccountSetup())
			return RedirectToPage("/Index");

		if (Profile.DateOfBirth.HasValue && Profile.DateOfBirth.Value.Date > DateTime.Today)
			ModelState.AddModelError("Profile.DateOfBirth", "Date of birth cannot be in the future.");
		if (User.IsInRole(UserRoles.Employee) && string.IsNullOrWhiteSpace(Profile.ClinicalRole))
			ModelState.AddModelError("Profile.ClinicalRole", "Select your clinical position.");

		if (RequiresPasswordChange)
		{
			if (Password is null || Password.Length < 12 || Password.Length > 128)
				ModelState.AddModelError(nameof(Password), "Choose a password between 12 and 128 characters.");
			if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
				ModelState.AddModelError(nameof(ConfirmPassword), "The passwords do not match.");
		}

		if (!ModelState.IsValid)
			return Page();

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		string? newPassword = RequiresPasswordChange ? Password : null;
		if (!await accounts.CompleteAccountSetupAsync(
			username,
			Profile.ToProfile(),
			newPassword,
			cancellationToken))
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			TempData["Message"] = "Account setup was already completed. Sign in again.";
			return RedirectToPage("/Login");
		}

		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				User.Claims.Where(claim =>
					claim.Type != UserAccountClaims.MustChangePassword &&
					claim.Type != UserAccountClaims.MustCompleteProfile),
				CookieAuthenticationDefaults.AuthenticationScheme));
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		TempData["Message"] = newPassword is null
			? "Profile saved."
			: "Profile and password saved.";
		return RedirectToPage("/Index");
	}

	private bool NeedsAccountSetup() =>
		User.HasClaim(UserAccountClaims.MustChangePassword, "true") ||
		User.HasClaim(UserAccountClaims.MustCompleteProfile, "true");
}
