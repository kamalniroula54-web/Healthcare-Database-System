using Microsoft.AspNetCore.Authentication.Cookies;
using MK_Hospital.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeFolder("/");
	options.Conventions.AllowAnonymousToPage("/Login");
	options.Conventions.AllowAnonymousToPage("/Setup");
	options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.Cookie.Name = "__Host-MKHospital.Auth";
		options.Cookie.HttpOnly = true;
		options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
			? CookieSecurePolicy.SameAsRequest
			: CookieSecurePolicy.Always;
		options.Cookie.SameSite = SameSiteMode.Strict;
		options.ExpireTimeSpan = TimeSpan.FromHours(8);
		options.SlidingExpiration = true;
		options.LoginPath = "/Login";
	});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<LocalDatabase>();
builder.Services.AddSingleton<IPatientRepository, SqlitePatientRepository>();
builder.Services.AddSingleton<IUserAccountService, UserAccountService>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<LocalAdminUser>,
		Microsoft.AspNetCore.Identity.PasswordHasher<LocalAdminUser>>();

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
app.UseAuthorization();
app.MapRazorPages();

app.Run();
