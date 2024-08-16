using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MonoTorrent
{
    static class TempDir
    {
        public readonly struct Releaser : IDisposable
        {
            public string Path { get; }

            public Releaser (string path)
                => Path = path;

            public void Dispose ()
            {
                Directory.Delete (Path, true);
            }
        }
        static SpinLocked<Random> RandomLocker = SpinLocked.Create (new Random ());
        public static Releaser Create ()
        {
            using (RandomLocker.Enter (out var random)) {
                var tmp = Path.Combine (
                    Path.GetTempPath (),
                    "monotorrent_tests",
                    random.Next (10000, 99999).ToString (),
                    $"_dir{Thread.CurrentThread.ManagedThreadId}-{Process.GetCurrentProcess ().Id}"
                );
                Directory.CreateDirectory (tmp);
                return new Releaser (tmp);
            }
        }
    }
}
