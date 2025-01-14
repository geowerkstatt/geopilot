using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
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

    public static User CreateUser(string authIdentifier, string fullName, string email, bool isAdmin = false)
        => new User
        {
            AuthIdentifier = authIdentifier,
            FullName = fullName,
            Email = email,
            IsAdmin = isAdmin,
        };

    public static void AssertResponseValueType(Type expectedValueType,  IActionResult response)
    {
        var objectResult = response as ObjectResult;
        Assert.IsNotNull(objectResult, "Response could not be cast to ObjectResult");
        var value = objectResult.Value;
        Assert.IsNotNull(value, "ObjectResult.Value should not be null");
        Assert.IsInstanceOfType(value, expectedValueType, $"Response.Value type was {value.GetType().Name} but was {expectedValueType.Name}");
    }
}
