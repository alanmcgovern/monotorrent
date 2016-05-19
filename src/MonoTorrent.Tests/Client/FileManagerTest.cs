using System;
using System.IO;
using System.Text;

namespace MonoTorrent.Tests.Client
{
    /// <summary>
    /// </summary>
    //
    public class FileManagerTest : IDisposable
    {
        private readonly string directoryName = string.Empty;
        private readonly string fullPath;
        private readonly string path = string.Empty;

        /// <summary>
        /// </summary>
        public FileManagerTest()
        {
            path = GetType().Assembly.Location;
            for (var i = 0; i >= 0; i++)
                if (!Directory.Exists("temp" + i))
                {
                    directoryName = "temp" + i;
                    fullPath = Path.Combine(path, directoryName);
                    Directory.CreateDirectory(fullPath);
                    break;
                }

            GenerateTestFiles();
        }

        /// <summary>
        /// </summary>
        public void Dispose()
        {
            foreach (var str in Directory.GetFiles(Path.Combine(path, directoryName)))
                File.Delete(str);

            Directory.Delete(Path.Combine(path, "temp"));
        }

        /// <summary>
        /// </summary>
        private void GenerateTestFiles()
        {
            var file1 = File.OpenWrite(Path.Combine(fullPath, "file1.txt"));
            var file2 = File.OpenWrite(Path.Combine(fullPath, "file2.txt"));

            var data =
                "this is my teststring. It's not really that long, but i'll be writing a lot more where this come from\r\n";

            for (var i = 0; i < 100; i++)
                file1.Write(Encoding.UTF8.GetBytes(data), 0, Encoding.UTF8.GetByteCount(data));

            for (var i = 0; i < 5000; i++)
                file2.Write(Encoding.UTF8.GetBytes(data), 0, Encoding.UTF8.GetByteCount(data));

            file1.Close();
            file2.Close();
        }
    }
}