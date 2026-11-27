using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

public sealed class ForgotPasswordModel(
	IUserAccountService accounts,
	IOutlookMailSender mailSender,
	ILogger<ForgotPasswordModel> logger) : PageModel
{
	[BindProperty]
	public PasswordResetRequestForm Input { get; set; } = new();

	[TempData]
	public bool RequestSubmitted { get; set; }

	[TempData]
	public string? SubmittedContactMethod { get; set; }

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (Input.ContactMethod is not ("Email" or "Phone"))
			ModelState.AddModelError("Input.ContactMethod", "Choose email or phone recovery.");

		if (!ModelState.IsValid)
			return Page();

		if (Input.ContactMethod == "Phone")
		{
			await accounts.RequestPhonePasswordResetAsync(Input.Username.Trim(), cancellationToken);
		}
		else
		{
			EmailPasswordReset? reset =
				await accounts.PrepareEmailPasswordResetAsync(Input.Username.Trim(), cancellationToken);
			if (reset is not null)
			{
				try
				{
					await mailSender.SendPasswordResetAsync(
						reset.Email,
						reset.Username,
						reset.FirstName,
						reset.TemporaryPassword,
						cancellationToken);
					if (!await accounts.CompleteEmailPasswordResetAsync(
						reset.RequestId,
						reset.TemporaryPassword,
						cancellationToken))
					{
						await accounts.MarkPasswordResetEmailFailedAsync(
							reset.RequestId,
							"PasswordResetCouldNotBeCompleted",
							CancellationToken.None);
						logger.LogError(
							"Password reset email was accepted but the password update failed for request {RequestId}.",
							reset.RequestId);
					}
				}
				catch (OutlookMailException exception)
				{
					await accounts.MarkPasswordResetEmailFailedAsync(
						reset.RequestId,
						exception.ErrorCode,
						CancellationToken.None);
					logger.LogError(
						"Password reset email delivery failed with {ErrorCode} for request {RequestId}.",
						exception.ErrorCode,
						reset.RequestId);
				}
				catch (OperationCanceledException)
				{
					await accounts.MarkPasswordResetEmailFailedAsync(
						reset.RequestId,
						"EmailRequestCancelled",
						CancellationToken.None);
					throw;
				}
			}
		}

		RequestSubmitted = true;
		SubmittedContactMethod = Input.ContactMethod;
		return RedirectToPage();
	}
}

public sealed class PasswordResetRequestForm
{
	[Required, StringLength(64, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;

	[Required]
	public string ContactMethod { get; set; } = string.Empty;
}
