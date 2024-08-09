using Geopilot.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Geopilot.Api;

internal static class ControllerExtensions
{
    public static Mock<HttpContext> SetupTestUser(this ControllerBase controller, User user)
    {
        var httpContextMock = new Mock<HttpContext>();
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        var principal = CreateClaimsPrincipal(user);
        httpContextMock.SetupGet(c => c.User).Returns(principal);
        return httpContextMock;
    }
}
