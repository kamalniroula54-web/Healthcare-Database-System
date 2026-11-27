using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class HospitalProfileForm
{
	[Required, StringLength(100, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;

	[StringLength(100)]
	public string LicenseNumber { get; set; } = string.Empty;

	[StringLength(1000)]
	public string Address { get; set; } = string.Empty;

	[StringLength(40)]
	public string ContactPhone { get; set; } = string.Empty;

	[EmailAddress, StringLength(254)]
	public string ContactEmail { get; set; } = string.Empty;

	public bool RemoveLogo { get; set; }
}
