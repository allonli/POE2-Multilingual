using System.Diagnostics;

namespace Poe2DbLookup.Services;

internal static class InteractionWaiter
{
    internal static bool WaitUntil(Func<bool> condition, TimeSpan timeout, TimeSpan pollInterval)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(pollInterval);
        }
        while (stopwatch.Elapsed < timeout);

        return condition();
    }
}
