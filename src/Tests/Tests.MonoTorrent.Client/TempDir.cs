using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var tmpDir = tmp + "_dir";
            Directory.CreateDirectory (tmpDir);
            File.Delete (tmp);
            return new Releaser (tmpDir);
        }
    }
}
