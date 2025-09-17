using System.Collections.Concurrent;

namespace Geopilot.Api;

/// <summary>
/// Provides extension methods for <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public static class ConcurrentDictionaryExtensions
{
    /// <summary>
    /// Attempts to update the value for the specified key using the provided update function, retrying up to maxRetries times.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The <see cref="ConcurrentDictionary{TKey, TValue}"/> to do the operation on.</param>
    /// <param name="key">The key of the value to update.</param>
    /// <param name="updateFunc">A function that takes the current value and returns the updated value. This function is possibly called up to <paramref name="maxRetries"/> times.</param>
    /// <param name="maxRetries">The maximum number of attempts to update the value in the dictionary.</param>
    /// <param name="updatedJob">When this method returns, contains the updated value if the update was successful; otherwise, the default value for the type.</param>
    public static bool TryUpdateWithRetry<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue, TValue> updateFunc,
        int maxRetries,
        out TValue updatedJob)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(updateFunc);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            if (dictionary.TryGetValue(key, out var currentValue))
            {
                var newValue = updateFunc(currentValue);
                if (dictionary.TryUpdate(key, newValue, currentValue))
                {
                    updatedJob = newValue;
                    return true;
                }
            }
        }

        updatedJob = default!;
        return false;
    }
}
