using Geopilot.Api.Validation;
using System.Collections.Concurrent;

namespace Geopilot.Api;

public static class ConcurrentDictionaryExtensions
{
    /// <summary>
    /// Attempts to update the value for the specified key using the provided update function, retrying up to maxRetries times.
    /// </summary>
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
