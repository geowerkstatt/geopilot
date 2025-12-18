using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Geopilot.Api;

internal class ActionResultAssert
{
    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is Ok (200).
    /// </summary>
    internal static void IsOk(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status200OK);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is OkObjectResult.
    /// </summary>
    /// <typeparam name="T">The expected type of the result object.</typeparam>
    /// <param name="actionResult">The <see cref="IActionResult"/> to be asserted.</param>
    /// <returns>The result object if the action result.</returns>
    /// <exception cref="AssertFailedException">Thrown if the action result is not an OkObjectResult or if the result object is not of type T.</exception>
    internal static T IsOkObjectResult<T>(IActionResult? actionResult)
        where T : class
    {
        IsOk(actionResult);
        var okResult = actionResult as OkObjectResult;
        Assert.IsNotNull(okResult, $"The action result is not an {nameof(OkObjectResult)}.");
        var resultObject = okResult.Value as T;
        Assert.IsNotNull(resultObject, $"The result object is not of the expected type {typeof(T)}.");
        return resultObject;
    }

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is Created (201).
    /// </summary>
    internal static void IsCreated(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status201Created);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is BadRequest (400).
    /// </summary>
    internal static void IsBadRequest(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status400BadRequest);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is Unauthorized (401).
    /// </summary>
    internal static void IsUnauthorized(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is NotFound (404).
    /// </summary>
    internal static void IsNotFound(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status404NotFound);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is InternalServerError (500).
    /// </summary>
    internal static void IsInternalServerError(IActionResult? actionResult)
        => AssertActionResult(actionResult, StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Asserts that the <see cref="IActionResult"/> is InternalServerError (500).
    /// </summary>
    internal static void IsInternalServerError(IActionResult? actionResult, string expectedErrorMessageSubstring)
    {
        AssertActionResult(actionResult, StatusCodes.Status500InternalServerError);

        var problemDetails = (ProblemDetails)((ObjectResult)actionResult!).Value!;
        Assert.Contains(
            expectedErrorMessageSubstring,
            problemDetails.Detail,
            StringComparison.OrdinalIgnoreCase,
            $"The error message does not contain the expected message '{expectedErrorMessageSubstring}'.");
    }

    private static void AssertActionResult(IActionResult? currentActionResult, int expectedStatusCode)
    {
        var statusCodeResult = currentActionResult as IStatusCodeActionResult;
        Assert.AreEqual(expectedStatusCode, statusCodeResult?.StatusCode, "Unexpected StatusCode of action result.");
    }
}
