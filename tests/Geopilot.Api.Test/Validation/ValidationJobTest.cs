using System.Collections.Immutable;

namespace Geopilot.Api.Validation;

[TestClass]
public class ValidationJobTest
{
    [TestMethod]
    [DataRow(
        Status.Completed,
        ValidatorResultStatus.Completed,
        ValidatorResultStatus.Completed,
        ValidatorResultStatus.Completed)
    ]
    [DataRow(
        Status.CompletedWithErrors,
        ValidatorResultStatus.Completed,
        ValidatorResultStatus.CompletedWithErrors,
        ValidatorResultStatus.Completed)
    ]
    [DataRow(
        Status.Processing,
        null,
        ValidatorResultStatus.CompletedWithErrors,
        ValidatorResultStatus.Completed)
    ]
    [DataRow(
        Status.Processing,
        null,
        ValidatorResultStatus.Failed,
        ValidatorResultStatus.Completed)
    ]
    [DataRow(
        Status.Failed,
        ValidatorResultStatus.Completed,
        ValidatorResultStatus.Failed,
        ValidatorResultStatus.Completed)
    ]
    public void GetStatusFromResults(Status expectedStatus, params ValidatorResultStatus?[] resultStatuses)
    {
        var validatorResults = resultStatuses
            .Select((status, i) => new KeyValuePair<string, ValidatorResult?>(
                $"Validator{i + 1}",
                status == null ? null : new ValidatorResult(status.Value, "some message")))
            .ToImmutableDictionary();

        var actualStatus = ValidationJob.GetStatusFromResults(validatorResults);

        Assert.AreEqual(expectedStatus, actualStatus);
    }

    [TestMethod]
    public void GetStatusFromResultsWithEmptyResultsThrowsException()
    {
        var validatorResults = ImmutableDictionary<string, ValidatorResult?>.Empty;
        Assert.ThrowsException<InvalidOperationException>(() =>
            ValidationJob.GetStatusFromResults(validatorResults));
    }

    [TestMethod]
    public void GetStatusFromResultsWithNullResultsThrowsException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            ValidationJob.GetStatusFromResults(null!));
    }
}
