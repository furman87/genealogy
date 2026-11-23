using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Genealogy.Data;

public sealed class ClubSignIn
{
    public const string VersionClaim = "club:session-version";
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 128;

    private readonly PasswordHasher<string> _hasher = new();
    private readonly string _dummyHash;

    public ClubSignIn()
    {
        _dummyHash = Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public string Hash(string password) => _hasher.HashPassword("genealogy", password);

    public bool Verify(ClubAccount? account, string password)
    {
        var result = _hasher.VerifyHashedPassword("genealogy", account?.PasswordHash ?? _dummyHash, password);
        return account?.IsActive == true && result != PasswordVerificationResult.Failed;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static async Task BootstrapAsync(ClubData data, ClubSignIn signIn, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password))
            return;

        if (string.IsNullOrEmpty(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email) ||
            password is null || string.IsNullOrWhiteSpace(password) ||
            password.Length is < MinimumPasswordLength or > MaximumPasswordLength)
        {
            throw new InvalidOperationException("Set a valid BootstrapAdmin email and a password of 12 to 128 characters.");
        }

        if (await data.BootstrapAdminAsync(NormalizeEmail(email), signIn.Hash(password)))
            logger.LogInformation("The first club administrator account was created.");
    }

    public static Task IssueCookieAsync(HttpContext context, ClubAccount account)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, account.Email),
            new(ClaimTypes.Role, account.Role),
            new(VersionClaim, account.SessionVersion.ToString(CultureInfo.InvariantCulture))
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return context.SignInAsync(principal);
    }

    public static async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        var accountId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var version = context.Principal?.FindFirstValue(VersionClaim);
        if (long.TryParse(accountId, out var id) && long.TryParse(version, out var expectedVersion))
        {
            var data = context.HttpContext.RequestServices.GetRequiredService<ClubData>();
            var account = await data.FindAccountAsync(id);
            if (account is { IsActive: true } && account.SessionVersion == expectedVersion)
                return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync();
    }
}
