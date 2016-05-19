using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MonoTorrent.Common
{
    public class TorrentFileSource : ITorrentFileSource
    {
        public IEnumerable<FileMapping> Files {
            get; private set;
        }

        public bool IgnoreHidden {
            get; private set;
        }

        public string Path {
            get; private set;
        }

        public string TorrentName {
            get; set;
        }

        public TorrentFileSource(string path)
            : this(path, true)
        {

        }

        public TorrentFileSource(string path, bool ignoreHidden)
        {
            IgnoreHidden = ignoreHidden;
            Path = path;
            LoadFiles();
        }

        private void LoadFiles()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            string fullPath = System.IO.Path.GetFullPath (Path);
            if (File.Exists (fullPath)) {
                TorrentName = System.IO.Path.GetFileName(fullPath);
                Files = new List<FileMapping> { new FileMapping(fullPath, TorrentName) };
                return;
            }

            if (!fullPath.EndsWith(sep.ToString()))
                fullPath += sep.ToString();

            // Process all directories and subdirectories of this folder
            // and add all the files to the 'files' list.
            List<string> files = new List<string> ();
            Queue<string> directories = new Queue<string> ();
            directories.Enqueue(fullPath);
            while (directories.Count > 0)
            {
                string current = directories.Dequeue ();
                if (IgnoreHidden) {
                    DirectoryInfo info = new DirectoryInfo (current);
                    if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                }

                foreach (string s in Directory.GetDirectories (current))
                    directories.Enqueue (s);

                files.AddRange (Directory.GetFiles (current));
            }

            // If we're ignoring hidden files, remove all the files with the hidden attribute
            if (IgnoreHidden) {
                files.RemoveAll (delegate (string file) {
                    return (new FileInfo (file).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                });
            }

            // Turn the full path of each file into a full path + relative path. The relative path is the 'path'
            // which the file will have within the .torrent metadata.
            Files = files.ConvertAll<FileMapping> (delegate (string file) {
                return new FileMapping(file, file.Substring(fullPath.Length));
            });

            // Set the torrent name (user can change it later)
            TorrentName = new DirectoryInfo(fullPath).Name;
        }
    }
}
