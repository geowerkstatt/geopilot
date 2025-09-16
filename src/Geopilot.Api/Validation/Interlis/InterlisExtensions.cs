namespace Geopilot.Api.Validation.Interlis;

public static class InterlisExtensions
{
    public static ValidatorResultStatus ToValidatorResultStatus(this InterlisStatusResponseStatus status)
    {
        return status switch
        {
            InterlisStatusResponseStatus.Completed => ValidatorResultStatus.Completed,
            InterlisStatusResponseStatus.CompletedWithErrors => ValidatorResultStatus.CompletedWithErrors,
            InterlisStatusResponseStatus.Failed => ValidatorResultStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }
}
