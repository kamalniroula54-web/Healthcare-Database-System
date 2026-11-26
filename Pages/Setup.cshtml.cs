using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

public sealed class SetupModel(IUserAccountService accounts) : PageModel
{
	[BindProperty, Required, StringLength(64, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;

	[BindProperty, Required, StringLength(128, MinimumLength = 12)]
	public string Password { get; set; } = string.Empty;

	[BindProperty, Required, Compare(nameof(Password))]
	public string ConfirmPassword { get; set; } = string.Empty;

	public string? ErrorMessage { get; private set; }

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
		await accounts.IsConfiguredAsync(cancellationToken)
			? RedirectToPage("/Login")
			: Page();

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return Page();

		if (!await accounts.CreateInitialAdminAsync(Username.Trim(), Password, cancellationToken))
		{
			ErrorMessage = "An administrator account already exists on this device. Please sign in.";
			return Page();
		}

		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				[new Claim(ClaimTypes.Name, Username.Trim())],
				CookieAuthenticationDefaults.AuthenticationScheme));
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		return RedirectToPage("/Index");
	}
}
