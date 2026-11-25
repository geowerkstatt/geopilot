using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Test.Processes;

/// <summary>
/// Test process with a secret: <c>token</c> may only come from the base configuration.
/// </summary>
public class SecretTokenProcess
{
    public SecretTokenProcess(string? host, [SecretConfigValue] string? token)
    {
        Host = host;
        Token = token;
    }

    public string? Host { get; }

    public string? Token { get; }

    [PipelineProcessRun]
    public Task<SecretTestResult> RunAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new SecretTestResult());
    }
}

/// <summary>
/// Test process that marks a number as secret, which the start refuses: only a text can be a secret.
/// </summary>
public class SecretNumberProcess
{
    public SecretNumberProcess([SecretConfigValue] int? pin)
    {
        Pin = pin;
    }

    public int? Pin { get; }

    [PipelineProcessRun]
    public Task<SecretTestResult> RunAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new SecretTestResult());
    }
}

/// <summary>
/// Result of the secret test processes.
/// </summary>
public class SecretTestResult
{
}
