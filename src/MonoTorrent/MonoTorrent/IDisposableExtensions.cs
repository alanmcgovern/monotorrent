using System;

namespace MonoTorrent
{
    static class IDisposableExtensions
    {
        public static void SafeDispose (this IDisposable disposable)
        {
            try {
                disposable.Dispose ();
            } catch {
                // Ignore
            }
        }
    }
}
