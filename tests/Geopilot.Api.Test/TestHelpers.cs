using Geopilot.Api.Models;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace Geopilot.Api;

internal static class TestHelpers
{
    /// <summary>
    /// Wraps a plain string as a <see cref="LocalizedText"/> with a single German entry for building test fixtures.
    /// </summary>
    /// <param name="text">The text to use as the German entry.</param>
    /// <returns>A localized text holding <paramref name="text"/> under the "de" language.</returns>
    public static LocalizedText Localized(string text) => new(new Dictionary<string, string> { { "de", text } });

    public static ClaimsPrincipal CreateClaimsPrincipal(User user)
        => new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                new Claim(JwtRegisteredClaimNames.Sub, user.AuthIdentifier),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName)
                ],
                "MockAuthentication"));

    public static User CreateUser(string authIdentifier, string fullName, string email, bool isAdmin = false, UserState state = UserState.Active)
        => new User
        {
            AuthIdentifier = authIdentifier,
            FullName = fullName,
            Email = email,
            IsAdmin = isAdmin,
            State = state,
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
            mandate = new Mandate() { Name = Localized(string.Join(' ', callerName, "Mandate")) };
        }

        mandate.Organisations.Add(organisation);
        organisation.Users.Add(user);
        context.Mandates.Add(mandate);
        context.SaveChanges();

        return (user, mandate);
    }

    /// <summary>
    /// Writes a run into the execution protocol only, the in-memory job store knows nothing of it. Without a
    /// <paramref name="terminalState"/> the run is alive (fresh heartbeat), otherwise its heartbeat is stale, like that
    /// of a run another instance executed, unless <paramref name="heartbeatAgo"/> says otherwise. With a
    /// <paramref name="submissionMandateKey"/> the run is a machine delivery attempt. Heartbeat and end come from the
    /// database clock.
    /// </summary>
    public static Guid AddRun(
        this Context context,
        int? mandateId,
        ProcessingState? terminalState = ProcessingState.Success,
        PermissionToDeliver permission = PermissionToDeliver.Permitted,
        TimeSpan? terminalAgo = null,
        string? submissionMandateKey = null,
        TimeSpan? heartbeatAgo = null)
    {
        var run = new Recording
        {
            JobId = Guid.NewGuid(),
            PipelineId = "pipe_a",
            Definition = "{}",
            AppVersion = "test",
            MandateId = mandateId,
            PermissionToDeliver = permission,
            UploadId = Guid.NewGuid(),
            UploadStorageLocation = "https://storage.example.com/uploads",
            UploadInitiatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            SubmissionMandateKey = submissionMandateKey,
            TerminalState = terminalState,
        };
        context.Recordings.Add(run);
        context.SaveChanges();

        var heartbeat = heartbeatAgo ?? (terminalState is null ? TimeSpan.Zero : TimeSpan.FromMinutes(10));
        var settledSince = terminalAgo ?? TimeSpan.Zero;
        context.Recordings
            .Where(r => r.Id == run.Id)
            .ExecuteUpdate(setters =>
            {
                setters.SetProperty(r => r.HeartbeatAt, r => DateTime.UtcNow - heartbeat);
                if (terminalState is not null)
                    setters.SetProperty(r => r.TerminalAt, r => DateTime.UtcNow - settledSince);
            });
        context.Entry(run).State = EntityState.Detached;
        return run.JobId;
    }
}
