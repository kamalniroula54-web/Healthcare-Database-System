using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

[Authorize(Roles = UserRoles.Staff)]
public sealed class LogoutModel : PageModel
{
	public async Task<IActionResult> OnPostAsync()
	{
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToPage("/Login");
	}
}
