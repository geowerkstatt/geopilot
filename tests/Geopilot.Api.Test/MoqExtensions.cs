using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test;

public static class MoqExtensions
{
    /// <summary>
    /// Verifies that the mocked logger received a log entry at the specified log level, containing all of the expected
    /// substrings in its message, exactly once.
    /// </summary>
    /// <typeparam name="T">The category type for the logger being mocked.</typeparam>
    /// <param name="fakeLogger">The mocked logger instance to verify.</param>
    /// <param name="logLevel">The log level to match when verifying the log entry.</param>
    /// <param name="expected">The substrings that must all be present in the logged message. Comparison is case-insensitive.</param>
    public static void VerifyMessageContains<T>(this Mock<ILogger<T>> fakeLogger, LogLevel logLevel, params string[] expected)
    {
        fakeLogger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    expected.All(s => o.ToString()!.Contains(s, StringComparison.OrdinalIgnoreCase))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
