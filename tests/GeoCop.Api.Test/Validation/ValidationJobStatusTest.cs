namespace GeoCop.Api.Validation;

[TestClass]
public class ValidationJobStatusTest
{
    [TestMethod]
    [DataRow(
        Status.Failed,
        Status.Completed,
        Status.CompletedWithErrors,
        Status.Failed)]
    [DataRow(
        Status.CompletedWithErrors,
        Status.Completed,
        Status.CompletedWithErrors,
        Status.Completed)]
    [DataRow(
        Status.Completed,
        Status.Completed,
        Status.Completed,
        Status.Completed)]
    [DataRow(
        Status.Processing,
        Status.Completed,
        Status.Processing,
        Status.CompletedWithErrors)]
    [DataRow(
        Status.Processing,
        Status.Failed,
        Status.Completed,
        Status.Processing)]
    public void UpdateJobStatusFromResults(Status expected, Status status1, Status status2, Status status3)
    {
        var status = new ValidationJobStatus(Guid.NewGuid());
        status.ValidatorResults.Add("Validator1", new ValidatorResult(status1, ""));
        status.ValidatorResults.Add("Validator2", new ValidatorResult(status2, ""));
        status.ValidatorResults.Add("Validator3", new ValidatorResult(status3, ""));

        status.UpdateJobStatusFromResults();

        Assert.AreEqual(expected, status.Status);
    }
}
