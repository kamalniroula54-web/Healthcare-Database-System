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

		UserAccount? account = await accounts.AuthenticateAsync(Username, Password, cancellationToken);
		if (account is null)
		{
			ErrorMessage = "The username or password is incorrect.";
			return Page();
		}

		List<Claim> claims =
		[
			new Claim(ClaimTypes.Name, account.Username),
			new Claim(ClaimTypes.Role, account.Role),
			new Claim(HospitalClaims.HospitalId, account.HospitalId.ToString())
		];
		if (account.IsHospitalAdmin)
			claims.Add(new Claim(ClaimTypes.Role, UserRoles.HospitalAdmin));
		if (account.CanApprovePatientAccess)
			claims.Add(new Claim(UserAccountClaims.CanApprovePatientAccess, "true"));
		if (account.MustChangePassword)
			claims.Add(new Claim(UserAccountClaims.MustChangePassword, "true"));
		if (!account.ProfileComplete)
			claims.Add(new Claim(UserAccountClaims.MustCompleteProfile, "true"));

		ClaimsPrincipal principal = new(
			new ClaimsIdentity(
				claims,
				CookieAuthenticationDefaults.AuthenticationScheme));

		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		if (account.MustChangePassword || !account.ProfileComplete)
			return RedirectToPage("/ChangePassword");

		return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
			? LocalRedirect(ReturnUrl)
			: RedirectToPage("/Index");
	}
}
