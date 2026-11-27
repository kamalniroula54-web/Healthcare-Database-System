using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MK_Hospital.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeFolder("/");
	options.Conventions.AllowAnonymousToPage("/Login");
	options.Conventions.AllowAnonymousToPage("/Setup");
	options.Conventions.AllowAnonymousToPage("/Error");
	options.Conventions.AllowAnonymousToPage("/AccessDenied");
	options.Conventions.AllowAnonymousToPage("/ForgotPassword");
	options.Conventions.AllowAnonymousToPage("/PatientPortal/Login");
	options.Conventions.AllowAnonymousToPage("/PatientPortal/Register");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.Cookie.Name = builder.Environment.IsDevelopment()
			? "MKHospital.Auth"
			: "__Host-MKHospital.Auth";
		options.Cookie.HttpOnly = true;
		options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
			? CookieSecurePolicy.SameAsRequest
			: CookieSecurePolicy.Always;
		options.Cookie.SameSite = SameSiteMode.Strict;
		options.ExpireTimeSpan = TimeSpan.FromHours(8);
		options.SlidingExpiration = true;
		options.LoginPath = "/Login";
		options.AccessDeniedPath = "/AccessDenied";
		options.Events.OnValidatePrincipal = async context =>
		{
			ClaimsPrincipal? principal = context.Principal;
			string? username = principal?.Identity?.Name;
			if (principal?.IsInRole(UserRoles.Patient) == true)
			{
				PatientPortalAuthenticationState? patientAccount = string.IsNullOrWhiteSpace(username)
					? null
					: await context.HttpContext.RequestServices
						.GetRequiredService<IPatientPortalService>()
						.GetAuthenticationStateAsync(username, context.HttpContext.RequestAborted);
				bool patientClaimsMatch = patientAccount is not null &&
					principal.FindFirstValue(ClaimTypes.Role) == UserRoles.Patient &&
					string.Equals(patientAccount.Username, username, StringComparison.OrdinalIgnoreCase) &&
					!principal.HasClaim(claim => claim.Type == HospitalClaims.HospitalId);
				if (!patientClaimsMatch)
				{
					context.RejectPrincipal();
					await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				}

				return;
			}

			AccountAuthenticationState? account = string.IsNullOrWhiteSpace(username)
				? null
				: await context.HttpContext.RequestServices
					.GetRequiredService<IUserAccountService>()
					.GetAuthenticationStateAsync(username, context.HttpContext.RequestAborted);
			string? hospitalIdClaim = principal?.FindFirstValue(HospitalClaims.HospitalId);
			bool hospitalClaimMatches = account is not null &&
				int.TryParse(hospitalIdClaim, out int hospitalId) &&
				(account.Role == UserRoles.Admin
					? await context.HttpContext.RequestServices
						.GetRequiredService<IHospitalService>()
						.ExistsAsync(hospitalId, context.HttpContext.RequestAborted)
					: account.HospitalId == hospitalId);

			bool claimsMatch = account is not null &&
				principal is not null &&
				account.Role == principal.FindFirstValue(ClaimTypes.Role) &&
				account.IsHospitalAdmin == principal.IsInRole(UserRoles.HospitalAdmin) &&
				account.CanApprovePatientAccess ==
					principal.HasClaim(UserAccountClaims.CanApprovePatientAccess, "true") &&
				account.MustChangePassword == principal.HasClaim(UserAccountClaims.MustChangePassword, "true") &&
				account.ProfileComplete == !principal.HasClaim(UserAccountClaims.MustCompleteProfile, "true") &&
				hospitalClaimMatches;
			if (!claimsMatch)
			{
				context.RejectPrincipal();
				await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			}
		};
		options.Events.OnRedirectToLogin = context =>
		{
			context.Response.Redirect(
				context.Request.Path.StartsWithSegments("/PatientPortal")
					? "/PatientPortal/Login"
					: context.RedirectUri);
			return Task.CompletedTask;
		};
	});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<LocalDatabase>();
builder.Services.AddScoped<CurrentHospitalContext>();
builder.Services.AddScoped<IPatientPortalService, PatientPortalService>();
builder.Services.AddScoped<IPatientRepository, SqlitePatientRepository>();
builder.Services.AddScoped<IPatientAccessService, SqlitePatientAccessService>();
builder.Services.AddScoped<IHospitalService, SqliteHospitalService>();
builder.Services.AddSingleton<IUserAccountService, UserAccountService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IOutlookMailSender, OutlookGraphMailSender>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<LocalAdminUser>,
		Microsoft.AspNetCore.Identity.PasswordHasher<LocalAdminUser>>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<PatientPortalUser>,
		Microsoft.AspNetCore.Identity.PasswordHasher<PatientPortalUser>>();

var app = builder.Build();
await app.Services.GetRequiredService<LocalDatabase>().InitializeAsync();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
	app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
	bool mustChangePassword = context.User.HasClaim(UserAccountClaims.MustChangePassword, "true");
	bool mustCompleteProfile = context.User.HasClaim(UserAccountClaims.MustCompleteProfile, "true");
	bool isAllowedDuringSetup = context.Request.Path.StartsWithSegments("/ChangePassword") ||
		context.Request.Path.StartsWithSegments("/Logout") ||
		context.Request.Path.StartsWithSegments("/phone-format") ||
		context.Request.Path.StartsWithSegments("/Error");
	if (context.User.Identity?.IsAuthenticated == true &&
		(mustChangePassword || mustCompleteProfile) &&
		!isAllowedDuringSetup)
	{
		context.Response.Redirect("/ChangePassword");
		return;
	}

	await next();
});
app.UseAuthorization();
app.MapPost("/phone-format", (PhoneFormatRequest request) =>
{
	if (request.Value?.Length > 64 ||
		!PhoneNumberSupport.TryFormatAsYouType(
			request.Value,
			request.RegionCode,
			out string formatted))
	{
		return Results.BadRequest();
	}

	return Results.Ok(new { formatted });
}).RequireAuthorization();
app.MapRazorPages();

app.Run();
