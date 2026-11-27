namespace MK_Hospital.Data;

public interface IOutlookMailSender
{
	Task<OutlookConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken);
	Task<OutlookConnectionStatus> StartConnectionAsync(CancellationToken cancellationToken);
	Task DisconnectAsync(CancellationToken cancellationToken);
	Task SendPasswordResetAsync(
		string recipientEmail,
		string username,
		string firstName,
		string temporaryPassword,
		CancellationToken cancellationToken);
}

public sealed class OutlookMailException(string errorCode, Exception? innerException = null)
	: Exception(errorCode, innerException)
{
	public string ErrorCode { get; } = errorCode;
}
