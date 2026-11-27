using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public sealed class HospitalsModel(IHospitalService hospitals, CurrentHospitalContext currentHospital) : PageModel
{
	[BindProperty]
	public HospitalForm Input { get; set; } = new();

	public IReadOnlyList<HospitalSummary> Hospitals { get; private set; } = [];
	public int CurrentHospitalId => currentHospital.HospitalId;

	public async Task OnGetAsync(CancellationToken cancellationToken) =>
		Hospitals = await hospitals.GetAllAsync(cancellationToken);

	public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			Hospitals = await hospitals.GetAllAsync(cancellationToken);
			return Page();
		}

		if (!await hospitals.CreateAsync(Input.Name, cancellationToken))
		{
			ModelState.AddModelError(nameof(Input.Name), "A hospital with that name already exists.");
			Hospitals = await hospitals.GetAllAsync(cancellationToken);
			return Page();
		}

		TempData["Message"] = "Hospital added. Switch to it using the Hospitals menu.";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostSwitchAsync(int hospitalId, CancellationToken cancellationToken)
	{
		if (!await hospitals.ExistsAsync(hospitalId, cancellationToken))
			return NotFound();

		List<Claim> claims = User.Claims
			.Where(claim => claim.Type != HospitalClaims.HospitalId)
			.ToList();
		claims.Add(new Claim(HospitalClaims.HospitalId, hospitalId.ToString()));
		ClaimsPrincipal principal = new(
			new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
		TempData["Message"] = "Active hospital changed.";
		return RedirectToPage("/Index");
	}
}

public sealed class HospitalForm
{
	[Required, StringLength(100, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;
}
