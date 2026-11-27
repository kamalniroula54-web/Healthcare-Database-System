using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
[RequestSizeLimit(HospitalLogoValidator.MaxLogoBytes + 65536)]
public sealed class EditHospitalModel(IHospitalService hospitals) : PageModel
{
	[BindProperty]
	public HospitalProfileForm Input { get; set; } = new();

	[BindProperty]
	public IFormFile? LogoFile { get; set; }

	public HospitalDetails? Hospital { get; private set; }

	public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
	{
		Hospital = await hospitals.GetDetailsAsync(id, cancellationToken);
		if (Hospital is null)
			return NotFound();

		Input = new HospitalProfileForm
		{
			Name = Hospital.Name,
			LicenseNumber = Hospital.LicenseNumber,
			Address = Hospital.Address,
			ContactPhone = Hospital.ContactPhone,
			ContactEmail = Hospital.ContactEmail
		};
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
	{
		byte[]? logoData = null;
		string? logoContentType = null;
		if (LogoFile is { Length: > 0 })
		{
			if (LogoFile.Length > HospitalLogoValidator.MaxLogoBytes)
			{
				ModelState.AddModelError(nameof(LogoFile), "The logo file must be 2 MB or smaller.");
			}
			else
			{
				await using Stream uploadStream = LogoFile.OpenReadStream();
				using MemoryStream buffer = new();
				await uploadStream.CopyToAsync(buffer, cancellationToken);
				if (buffer.Length > HospitalLogoValidator.MaxLogoBytes)
				{
					ModelState.AddModelError(nameof(LogoFile), "The logo file must be 2 MB or smaller.");
				}
				else
				{
					logoData = buffer.ToArray();
					if (!HospitalLogoValidator.TryGetContentType(logoData, out logoContentType))
					{
						logoData = null;
						ModelState.AddModelError(
							nameof(LogoFile),
							"Upload a valid PNG or JPEG image no larger than 4096 by 4096 pixels.");
					}
				}
			}
		}

		if (Input.RemoveLogo && LogoFile is { Length: > 0 })
			ModelState.AddModelError(nameof(Input.RemoveLogo), "Choose either a new logo or removal, not both.");

		Hospital = await hospitals.GetDetailsAsync(id, cancellationToken);
		if (Hospital is null)
			return NotFound();

		if (!ModelState.IsValid)
			return Page();

		if (!await hospitals.UpdateDetailsAsync(
			id,
			Input,
			logoData,
			logoContentType,
			cancellationToken))
		{
			ModelState.AddModelError(string.Empty, "Hospital details could not be saved. The name may already be in use.");
			return Page();
		}

		TempData["Message"] = "Hospital details saved.";
		return RedirectToPage("/Admin/Hospitals");
	}

}
