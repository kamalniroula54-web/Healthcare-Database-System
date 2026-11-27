using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.PatientPortal;

[AllowAnonymous]
public sealed class LoginModel(IPatientPortalService patientPortal) : PageModel
{
	[BindProperty, Required, StringLength(64)]
	public string Username { get; set; } = string.Empty;

	[BindProperty, Required, StringLength(128)]
	public string Password { get; set; } = string.Empty;

	public string? ErrorMessage { get; private set; }

	public void OnGet()
	{
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return Page();

		PatientPortalUser? account =
			await patientPortal.AuthenticateAsync(Username, Password, cancellationToken);
		if (account is null)
		{
			ErrorMessage = "The username or password is incorrect.";
			return Page();
		}

		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				[
					new Claim(ClaimTypes.Name, account.Username),
					new Claim(ClaimTypes.Role, UserRoles.Patient)
				],
				CookieAuthenticationDefaults.AuthenticationScheme));
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		return RedirectToPage("/PatientPortal/Records");
	}
}
