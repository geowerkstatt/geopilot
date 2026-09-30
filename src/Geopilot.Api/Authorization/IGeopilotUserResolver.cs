using Geopilot.Api.Contracts;
using Geopilot.Api.Models;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Resolves the current request to the <see cref="User"/> behind its token.
/// </summary>
public interface IGeopilotUserResolver
{
    /// <summary>
    /// Resolves the user of the current request, creating the account on first sign-in and refreshing its
    /// name and email from the identity provider otherwise. Returns <see langword="null"/> when the request
    /// carries no token, when the identity provider does not know the token as a person, or when the token
    /// belongs to a registered machine client, which is never a user.
    /// </summary>
    Task<User?> ResolveAsync();

    /// <summary>
    /// Requests the user info of a person's token while the request is still being authenticated, so an
    /// unreachable identity provider surfaces there as <see cref="IdentityProviderUnavailableException"/>
    /// instead of as a refusal by an authorization handler. The response stays with the scoped user info
    /// service for <see cref="ResolveAsync"/>. Does nothing for a subject registered as a machine client:
    /// its token names no person, and most identity providers answer a failure when asked for one.
    /// </summary>
    /// <param name="subject">The subject the token was authenticated as, if it carries one. Without one, the user
    /// info is always requested: it is then the only place the subject of a person's token can come from.</param>
    /// <param name="accessToken">The access token of the request.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The user info the identity provider describes for the token, or <see langword="null"/> when the
    /// subject is a registered machine client or the identity provider describes no person for the token.</returns>
    Task<UserInfoResponse?> PrefetchUserInfoAsync(string? subject, string accessToken, CancellationToken cancellationToken);
}
