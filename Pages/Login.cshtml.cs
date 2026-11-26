using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

public sealed class LoginModel(IUserAccountService accounts) : PageModel
{
	[BindProperty]
	public string Username { get; set; } = string.Empty;

	[BindProperty]
	public string Password { get; set; } = string.Empty;

	[BindProperty]
	public string? ReturnUrl { get; set; }

	public string? ErrorMessage { get; private set; }

	public async Task<IActionResult> OnGetAsync(string? returnUrl, CancellationToken cancellationToken)
	{
		ReturnUrl = returnUrl;
		if (!await accounts.IsConfiguredAsync(cancellationToken))
			return RedirectToPage("/Setup");
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!await accounts.IsConfiguredAsync(cancellationToken))
			return RedirectToPage("/Setup");

		if (!await accounts.VerifyCredentialsAsync(Username, Password, cancellationToken))
		{
			ErrorMessage = "The username or password is incorrect.";
			return Page();
		}

		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				[new Claim(ClaimTypes.Name, Username.Trim())],
				CookieAuthenticationDefaults.AuthenticationScheme));

		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
			? LocalRedirect(ReturnUrl)
			: RedirectToPage("/Index");
	}
}
