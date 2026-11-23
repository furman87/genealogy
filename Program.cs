using System.Net;
using System.Threading.RateLimiting;
using Genealogy.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var storage = new ClubStorage(builder.Configuration, builder.Environment);
var webRoot = Path.GetFullPath(builder.Environment.WebRootPath ??
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
var relativeDataPath = Path.GetRelativePath(webRoot, storage.Root);
if (!Path.IsPathRooted(relativeDataPath) && relativeDataPath != ".." &&
    !relativeDataPath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
{
    throw new InvalidOperationException("Club storage must be outside the public web root.");
}

storage.Prepare();
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton<ClubData>();
builder.Services.AddSingleton<ClubClock>();
builder.Services.AddSingleton<ClubSignIn>();
builder.Services.AddSingleton<ResourceFiles>();
builder.Services.AddDataProtection().SetApplicationName("Colonial Heritage Genealogy")
    .PersistKeysToFileSystem(new DirectoryInfo(storage.KeyDirectory));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = ClubStorage.MaxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = ClubStorage.MaxUploadBytes);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "Genealogy.Session";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/sign-in";
    options.AccessDeniedPath = "/account/denied";
    options.Events.OnValidatePrincipal = ClubSignIn.ValidateCookieAsync;
});
builder.Services.AddAuthorization(options =>
    options.AddPolicy("ClubAdmin", policy => policy.RequireRole(AccountRoles.Admin)));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sign-in", context =>
        HttpMethods.IsPost(context.Request.Method) &&
        !string.Equals(context.Request.Query["handler"], "SignOut", StringComparison.OrdinalIgnoreCase)
            ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(5) })
            : RateLimitPartition.GetNoLimiter("get"));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "ClubAdmin");
    options.Conventions.AuthorizePage("/Account/Password");
});

var proxyAddress = builder.Configuration["Club:ProxyAddress"];
if (!string.IsNullOrWhiteSpace(proxyAddress))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(IPAddress.Parse(proxyAddress));
    });
}

var app = builder.Build();
await app.Services.GetRequiredService<ClubData>().InitializeAsync();
await ClubSignIn.BootstrapAsync(app.Services.GetRequiredService<ClubData>(),
    app.Services.GetRequiredService<ClubSignIn>(), app.Configuration, app.Logger);

if (!string.IsNullOrWhiteSpace(proxyAddress))
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    await next(context);
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || context.Request.Path.StartsWithSegments("/admin"))
        context.Response.Headers.CacheControl = "no-store";

    await next(context);
});
app.UseAuthorization();
app.MapRazorPages();
await app.RunAsync();
