using GeoCop.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace GeoCop.Api.Test;

internal static class ControllerExtensions
{
    public static void SetupTestUser(this ControllerBase controller, User user)
    {
        var httpContextMock = new Mock<HttpContext>();
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ContextExtensions.UserIdClaim, user.AuthIdentifier),
            new Claim(ContextExtensions.NameClaim, user.FullName),
            new Claim(ContextExtensions.EmailClaim, user.Email),
        }));
        httpContextMock.SetupGet(c => c.User).Returns(principal);
    }
}
