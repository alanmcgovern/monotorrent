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

        public static Releaser Create ()
        {
            var tmp = Path.GetTempFileName ();
            var tmpDir = tmp + $"_dir{Thread.CurrentThread.ManagedThreadId}-{Process.GetCurrentProcess ().Id}";
            Directory.CreateDirectory (tmpDir);
            File.Delete (tmp);
            return new Releaser (tmpDir);
        }
    }
}
