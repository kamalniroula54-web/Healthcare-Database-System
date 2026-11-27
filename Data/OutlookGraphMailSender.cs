using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace MK_Hospital.Data;

public sealed class OutlookGraphMailSender(
	IConfiguration configuration,
	LocalDatabase database,
	IHttpClientFactory httpClientFactory,
	ILogger<OutlookGraphMailSender> logger) : IOutlookMailSender
{
	public const string ExpectedSenderEmail = "passwordresethospital@outlook.com";

	private static readonly string[] Scopes = ["Mail.Send"];
	private readonly SemaphoreSlim initializationLock = new(1, 1);
	private readonly SemaphoreSlim connectionStartLock = new(1, 1);
	private readonly object stateLock = new();
	private IPublicClientApplication? application;
	private OutlookConnectionStatus? connectionState;
	private Task<AuthenticationResult>? connectionTask;

	public async Task<OutlookConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken)
	{
		if (!IsConfigured)
			return new(false, false, null, null, null, null, "ClientIdMissing");

		lock (stateLock)
		{
			if (connectionState is not null)
			{
				if (connectionState.IsConnected && !IsExpectedSender(connectionState.ConnectedEmail))
				{
					return new(
						true,
						false,
						connectionState.ConnectedEmail,
						null,
						null,
						null,
						"SenderMailboxMismatch");
				}

				return connectionState;
			}
		}

		IPublicClientApplication client = await GetApplicationAsync(cancellationToken);
		IAccount[] accounts = (await client.GetAccountsAsync()).ToArray();
		IAccount? account = accounts.FirstOrDefault(IsExpectedSender);
		if (account is null)
		{
			IAccount? otherAccount = accounts.FirstOrDefault();
			return otherAccount is null
				? new(true, false, null, null, null, null, null)
				: new(true, false, otherAccount.Username, null, null, null, "SenderMailboxMismatch");
		}

		try
		{
			AuthenticationResult token = await client.AcquireTokenSilent(Scopes, account)
				.ExecuteAsync(cancellationToken);
			return new(true, true, token.Account.Username, null, null, null, null);
		}
		catch (MsalUiRequiredException)
		{
			return new(true, false, account.Username, null, null, null, "ReconnectionRequired");
		}
	}

	public async Task<OutlookConnectionStatus> StartConnectionAsync(CancellationToken cancellationToken)
	{
		if (!IsConfigured)
			return new(false, false, null, null, null, null, "ClientIdMissing");

		await connectionStartLock.WaitAsync(cancellationToken);
		try
		{
			OutlookConnectionStatus current = await GetConnectionStatusAsync(cancellationToken);
			if (current.IsConnected)
				return current;
			if (current.ErrorCode == "SenderMailboxMismatch")
				return current;

			lock (stateLock)
			{
				if (connectionTask is { IsCompleted: false } && connectionState is not null)
					return connectionState;
			}

			IPublicClientApplication client = await GetApplicationAsync(cancellationToken);
			TaskCompletionSource<bool> codeReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
			lock (stateLock)
				connectionState = new(true, false, null, null, null, "Waiting for Microsoft sign-in details.", null);

			Task<AuthenticationResult> pendingConnection = client.AcquireTokenWithDeviceCode(
				Scopes,
				result =>
				{
					lock (stateLock)
					{
						connectionState = new(
							true,
							false,
							null,
							result.UserCode,
							result.VerificationUrl,
							result.Message,
							null);
					}

					codeReceived.TrySetResult(true);
					return Task.CompletedTask;
				}).ExecuteAsync();

			lock (stateLock)
				connectionTask = pendingConnection;

			_ = ObserveConnectionAsync(pendingConnection);
			await Task.WhenAny(codeReceived.Task, pendingConnection);
			lock (stateLock)
				return connectionState ?? new(true, false, null, null, null, null, "ConnectionFailed");
		}
		finally
		{
			connectionStartLock.Release();
		}
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		IPublicClientApplication client = await GetApplicationAsync(cancellationToken);
		await connectionStartLock.WaitAsync(cancellationToken);
		try
		{
			lock (stateLock)
			{
				if (connectionTask is { IsCompleted: false })
					throw new InvalidOperationException("Complete or cancel the pending Outlook sign-in before disconnecting.");
			}

			foreach (IAccount account in await client.GetAccountsAsync())
				await client.RemoveAsync(account);

			lock (stateLock)
			{
				connectionState = null;
				connectionTask = null;
			}
		}
		finally
		{
			connectionStartLock.Release();
		}
	}

	public async Task SendPasswordResetAsync(
		string recipientEmail,
		string username,
		string firstName,
		string temporaryPassword,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(recipientEmail))
			throw new OutlookMailException("EmailAddressMissing");

		if (!IsConfigured)
			throw new OutlookMailException("ClientIdMissing");

		IPublicClientApplication client = await GetApplicationAsync(cancellationToken);
		IAccount[] accounts = (await client.GetAccountsAsync()).ToArray();
		IAccount? account = accounts.FirstOrDefault(IsExpectedSender);
		if (account is null)
			throw new OutlookMailException(
				accounts.Length > 0 ? "SenderMailboxMismatch" : "OutlookNotConnected");

		AuthenticationResult token;
		try
		{
			token = await client.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken);
		}
		catch (MsalUiRequiredException exception)
		{
			throw new OutlookMailException("ReconnectionRequired", exception);
		}
		catch (MsalException exception)
		{
			throw new OutlookMailException(exception.ErrorCode, exception);
		}

		string greeting = string.IsNullOrWhiteSpace(firstName) ? "Hello" : $"Hello {firstName}";
		string body =
			$"""
			{greeting},

			We received a password reset request for your MK Hospital account ({username}).
			Your one-time password (OTP) is: {temporaryPassword}

			Sign in with this OTP and choose a new password. If you did not request this reset, contact your administrator.
			""";
		object request = new
		{
			message = new
			{
				subject = "MK Hospital password reset",
				body = new { contentType = "Text", content = body },
				toRecipients = new[]
				{
					new { emailAddress = new { address = recipientEmail } }
				}
			}
		};

		using HttpRequestMessage message = new(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/sendMail")
		{
			Content = JsonContent.Create(request)
		};
		message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

		try
		{
			using HttpResponseMessage response = await httpClientFactory.CreateClient()
				.SendAsync(message, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				string errorCode = $"GraphHttp{(int)response.StatusCode}";
				logger.LogError("Microsoft Graph rejected a password reset email with {ErrorCode}.", errorCode);
				throw new OutlookMailException(errorCode);
			}
		}
		catch (HttpRequestException exception)
		{
			logger.LogError(exception, "Microsoft Graph could not deliver a password reset email.");
			throw new OutlookMailException("GraphUnavailable", exception);
		}
	}

	private bool IsConfigured =>
		!string.IsNullOrWhiteSpace(configuration["Outlook:ClientId"]);

	private static bool IsExpectedSender(IAccount account) =>
		IsExpectedSender(account.Username);

	private static bool IsExpectedSender(string? email) =>
		string.Equals(email?.Trim(), ExpectedSenderEmail, StringComparison.OrdinalIgnoreCase);

	private async Task<IPublicClientApplication> GetApplicationAsync(CancellationToken cancellationToken)
	{
		if (application is not null)
			return application;

		await initializationLock.WaitAsync(cancellationToken);
		try
		{
			if (application is not null)
				return application;

			string clientId = configuration["Outlook:ClientId"]
				?? throw new OutlookMailException("ClientIdMissing");
			StorageCreationProperties storageProperties = new StorageCreationPropertiesBuilder(
				"mk-hospital-outlook.msalcache",
				database.DataDirectory).Build();
			MsalCacheHelper cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
			IPublicClientApplication client = PublicClientApplicationBuilder.Create(clientId)
				.WithAuthority("https://login.microsoftonline.com/consumers")
				.Build();
			cacheHelper.RegisterCache(client.UserTokenCache);
			application = client;
			return client;
		}
		finally
		{
			initializationLock.Release();
		}
	}

	private async Task ObserveConnectionAsync(Task<AuthenticationResult> pendingConnection)
	{
		try
		{
			AuthenticationResult token = await pendingConnection;
			lock (stateLock)
			{
				bool isExpectedSender = IsExpectedSender(token.Account.Username);
				connectionState = new(
					true,
					isExpectedSender,
					token.Account.Username,
					null,
					null,
					null,
					isExpectedSender ? null : "SenderMailboxMismatch");
			}
		}
		catch (MsalException exception)
		{
			logger.LogError("Outlook sign-in failed with {ErrorCode}.", exception.ErrorCode);
			lock (stateLock)
				connectionState = new(true, false, null, null, null, null, exception.ErrorCode);
		}
		catch (OperationCanceledException)
		{
			lock (stateLock)
				connectionState = new(true, false, null, null, null, null, "ConnectionCancelled");
		}
	}
}
