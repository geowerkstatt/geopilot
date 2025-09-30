using Geopilot.Api.Controllers;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Creates a user, an organisation and a mandate and links them together so that the user is authorized for the mandate.
    /// </summary>
    /// <param name="context">Context to use for the setup.</param>
    /// <param name="mandate">Optional override of the default mandate.</param>
    /// <param name="callerName">Optional marker for the created Objects used for all descriptive Name values.</param>
    /// <returns></returns>
    public static (User User, Mandate Mandate) AddMandateWithUserOrganisation(this Context context, Mandate? mandate = null, [CallerMemberName] string callerName = "")
    {
        var user = new User() { FullName = string.Join(' ', callerName, "User") };
        var organisation = new Organisation() { Name = string.Join(' ', callerName, "Organisation") };

        if (mandate == null)
        {
            mandate = new Mandate() { Name = string.Join(' ', callerName, "Mandate") };
        }

        mandate.Organisations.Add(organisation);
        organisation.Users.Add(user);
        context.Mandates.Add(mandate);
        context.SaveChanges();

        return (user, mandate);
    }
}
