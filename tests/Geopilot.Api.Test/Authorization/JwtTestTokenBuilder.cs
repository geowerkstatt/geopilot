using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Geopilot.Api.Authorization;

internal static class JwtTestTokenBuilder
{
    public const string Issuer = "https://jwt-test-issuer";
    public const string Audience = "geopilot-api";
    public const string AdminSub = "1f9f9000-c651-4b04-b6ae-9ce1e7f45c15";
    public const string UserSub = "1ed45832-2880-4fd4-a274-bbcc101c3307";

    private static readonly RSA RsaKey = RSA.Create(2048);
    public static readonly RsaSecurityKey SigningKey = new(RsaKey);
    public static readonly SigningCredentials Credentials = new(SigningKey, SecurityAlgorithms.RsaSha256);

    private static readonly RSA WrongRsaKey = RSA.Create(2048);

    public static string CreateToken(
        string sub,
        string issuer,
        string audience,
        SigningCredentials creds,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var now = DateTime.UtcNow;
        var (email, name) = sub switch
        {
            AdminSub => ("admin@geopilot.ch", "Andreas Admin"),
            UserSub => ("user@geopilot.ch", "Ursula User"),
            _ => ("unknown@geopilot.ch", "Unknown User"),
        };

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, name),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore ?? now,
            expires: expires ?? now.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreateValidAdminToken() =>
        CreateToken(AdminSub, Issuer, Audience, Credentials);

    public static string CreateValidUserToken() =>
        CreateToken(UserSub, Issuer, Audience, Credentials);

    public static string CreateExpiredToken() =>
        CreateToken(AdminSub, Issuer, Audience, Credentials, expires: DateTime.UtcNow.AddHours(-1));

    public static string CreateFutureNbfToken() =>
        CreateToken(AdminSub, Issuer, Audience, Credentials, notBefore: DateTime.UtcNow.AddHours(1));

    public static string CreateWrongAudienceToken() =>
        CreateToken(AdminSub, Issuer, "wrong-audience", Credentials);

    public static string CreateWrongIssuerToken() =>
        CreateToken(AdminSub, "https://wrong-issuer", Audience, Credentials);

    public static string CreateWrongKeyToken() =>
        CreateToken(AdminSub, Issuer, Audience,
            new SigningCredentials(new RsaSecurityKey(WrongRsaKey), SecurityAlgorithms.RsaSha256));

    public static string CreateTamperedToken()
    {
        var token = CreateValidAdminToken();
        var parts = token.Split('.');
        var signatureBytes = Base64UrlDecode(parts[2]);
        signatureBytes[^1] ^= 0xFF;
        parts[2] = Base64UrlEncode(signatureBytes);
        return string.Join('.', parts);
    }

    public static string CreateAlgNoneToken(string alg = "none")
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes($"{{\"alg\":\"{alg}\",\"typ\":\"JWT\"}}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(BuildClaimsPayload(AdminSub)));
        return $"{header}.{payload}.";
    }

    public static string CreateHS256KeyConfusionToken()
    {
        var publicKeyBytes = RsaKey.ExportSubjectPublicKeyInfo();
        var hmacKey = new SymmetricSecurityKey(publicKeyBytes);
        var hmacCreds = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(BuildClaimsPayload(AdminSub)));
        var signingInput = $"{header}.{payload}";

        using var hmac = new HMACSHA256(publicKeyBytes);
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string BuildClaimsPayload(string sub)
    {
        var now = DateTimeOffset.UtcNow;
        var (email, name) = sub switch
        {
            AdminSub => ("admin@geopilot.ch", "Andreas Admin"),
            UserSub => ("user@geopilot.ch", "Ursula User"),
            _ => ("unknown@geopilot.ch", "Unknown User"),
        };

        return $"{{\"sub\":\"{sub}\",\"email\":\"{email}\",\"name\":\"{name}\"," +
               $"\"nbf\":{now.ToUnixTimeSeconds()},\"exp\":{now.AddHours(1).ToUnixTimeSeconds()}," +
               $"\"iss\":\"{Issuer}\",\"aud\":\"{Audience}\"}}";
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
