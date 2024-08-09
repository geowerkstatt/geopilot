using Geopilot.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Geopilot.Api;

internal static class TestHelpers
{
    public static ClaimsPrincipal CreateClaimsPrincipal(User user)
        => new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.AuthIdentifier),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
        }));
}
