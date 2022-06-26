//
// TorrentFileSource.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System.Collections.Generic;
using System.IO;

namespace MonoTorrent
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

        public TorrentFileSource (string path)
            : this (path, true)
        {

        }

        public TorrentFileSource (string path, bool ignoreHidden)
        {
            IgnoreHidden = ignoreHidden;
            Path = path;

            string fullPath = System.IO.Path.GetFullPath (Path);
            if (File.Exists (fullPath)) {
                TorrentName = System.IO.Path.GetFileName (fullPath);
                Files = new List<FileMapping> { new FileMapping (fullPath, TorrentName, new FileInfo (fullPath).Length) };
                return;
            }

            char sep = System.IO.Path.DirectorySeparatorChar;
            if (!fullPath.EndsWith (sep.ToString ()))
                fullPath += sep.ToString ();

            // Process all directories and subdirectories of this folder
            // and add all the files to the 'files' list.
            var files = new List<string> ();
            var directories = new Queue<string> ();
            directories.Enqueue (fullPath);
            while (directories.Count > 0) {
                string current = directories.Dequeue ();
                if (IgnoreHidden) {
                    var info = new DirectoryInfo (current);
                    if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                }

                foreach (string s in Directory.GetDirectories (current))
                    directories.Enqueue (s);

                files.AddRange (Directory.GetFiles (current));
            }

            // If we're ignoring hidden files, remove all the files with the hidden attribute
            if (IgnoreHidden) {
                files.RemoveAll (file =>
                    (new FileInfo (file).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden);
            }

            // Turn the full path of each file into a full path + relative path. The relative path is the 'path'
            // which the file will have within the .torrent metadata.
            Files = files.ConvertAll (file => new FileMapping (file, file.Substring (fullPath.Length), new FileInfo (file).Length));

            // Set the torrent name (user can change it later)
            TorrentName = new DirectoryInfo (fullPath).Name;
        }
    }
}
