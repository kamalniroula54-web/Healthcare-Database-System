using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientPortalRegistrationForm
{
	[Required, StringLength(64, MinimumLength = 3)]
	[RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$")]
	public string Username { get; set; } = string.Empty;

	[Required, StringLength(128, MinimumLength = 12)]
	public string Password { get; set; } = string.Empty;

	[Required, Compare(nameof(Password))]
	public string ConfirmPassword { get; set; } = string.Empty;

	[Required, StringLength(64, MinimumLength = 20)]
	public string RegistrationCode { get; set; } = string.Empty;
}
