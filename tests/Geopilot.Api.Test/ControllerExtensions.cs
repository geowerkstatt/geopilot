using Geopilot.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Geopilot.Api;

internal static class ControllerExtensions
{
    public static Mock<HttpContext> SetupTestUser(this ControllerBase controller, User user)
    {
        var httpContextMock = new Mock<HttpContext>();
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.AuthIdentifier),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
        }));
        httpContextMock.SetupGet(c => c.User).Returns(principal);
        return httpContextMock;
    }
}
