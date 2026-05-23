using tci.FileFlow.SftpEngine.Core.Models;
using tci.FileFlow.SftpEngine.Core.Services;
using tci.FileFlow.SftpEngine.Core.BackgroundServices;
using MudBlazor.Services;
using Blazored.LocalStorage;

var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isDevelopment ? Directory.GetCurrentDirectory() : AppContext.BaseDirectory
};
var builder = WebApplication.CreateBuilder(options);

// Enable native Windows Service support
builder.Host.UseWindowsService();
// Add Blazor Server and Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();

// --- MULTI-LANGUAGE (LOCALIZATION) SETUP ---
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Register our Core Infrastructure Layers
builder.Services.AddSingleton<TransferProgressState>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<ISftpService, SftpService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register the 24/7 background worker execution thread
builder.Services.AddHostedService<FileFlowWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// --- CONFIGURE CHOSEN CULTURE (SPANISH DEFAULT) ---
var supportedCultures = new[] { "es", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0]) // "es" is the default driver
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.MapGet("/Culture/Set", (string culture, string redirectUri, HttpContext context) =>
{
    if (culture != null)
    {
        var cookieName = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName;
        var cookieValue = Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
            new Microsoft.AspNetCore.Localization.RequestCulture(culture, culture));
        context.Response.Cookies.Append(cookieName, cookieValue, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
    }
    return Results.LocalRedirect(redirectUri);
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();