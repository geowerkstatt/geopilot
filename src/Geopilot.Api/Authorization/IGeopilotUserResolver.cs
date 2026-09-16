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
}
