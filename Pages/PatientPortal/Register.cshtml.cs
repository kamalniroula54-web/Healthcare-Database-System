using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.PatientPortal;

[AllowAnonymous]
public sealed class RegisterModel(IPatientPortalService patientPortal) : PageModel
{
	[BindProperty]
	public PatientPortalRegistrationForm Input { get; set; } = new();

	public string? ErrorMessage { get; private set; }

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return Page();

		PatientPortalRegistrationStatus status = await patientPortal.RegisterAsync(
			Input.RegistrationCode,
			Input.Username,
			Input.Password,
			cancellationToken);
		if (status == PatientPortalRegistrationStatus.UsernameUnavailable)
		{
			ModelState.AddModelError("Input.Username", "That username is unavailable. Choose another.");
			return Page();
		}

		if (status != PatientPortalRegistrationStatus.Succeeded)
		{
			ErrorMessage = "The code is invalid, expired, or already used. Contact the hospital that issued it.";
			return Page();
		}

		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				[
					new Claim(ClaimTypes.Name, Input.Username.Trim()),
					new Claim(ClaimTypes.Role, UserRoles.Patient)
				],
				CookieAuthenticationDefaults.AuthenticationScheme));
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		return RedirectToPage("/PatientPortal/Records");
	}
}
