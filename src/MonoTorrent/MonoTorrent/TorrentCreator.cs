//
// TorrentCreator.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006-2007 Gregor Burger and Alan McGovern
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent
{
    public class TorrentCreator : EditableTorrent
    {
        public static int RecommendedPieceSize (long totalSize)
        {
            // Check all piece sizes that are multiples of 32kB and
            // choose the smallest piece size which results in a
            // .torrent file smaller than 60kb
            for (int i = 32768; i < 4 * 1024 * 1024; i *= 2) {
                int pieces = (int) (totalSize / i) + 1;
                if ((pieces * 20) < (60 * 1024))
                    return i;
            }

            // If we get here, we're hashing a massive file, so lets limit
            // to a max of 4MB pieces.
            return 4 * 1024 * 1024;
        }

        public static int RecommendedPieceSize (IEnumerable<string> files)
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f).Length));

        public static int RecommendedPieceSize (IEnumerable<TorrentFile> files)
            => RecommendedPieceSize (files.Sum (f => f.Length));

        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
            => RecommendedPieceSize (files.Sum (f => new FileInfo (f.Source).Length));

        public event EventHandler<TorrentCreatorEventArgs> Hashed;

        public List<string> GetrightHttpSeeds { get; }
        public bool StoreMD5 { get; set; }

        public TorrentCreator ()
        {
            GetrightHttpSeeds = new List<string> ();
            CanEditSecureMetadata = true;
            CreatedBy = string.Format ("MonoTorrent {0}", VersionInfo.Version);
        }

        public BEncodedDictionary Create (ITorrentFileSource fileSource)
            => Create (fileSource, CancellationToken.None);

        public Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource)
            => Task.Run (() => Create (fileSource, CancellationToken.None));

        public Task<BEncodedDictionary> CreateAsync (ITorrentFileSource fileSource, CancellationToken token)
            => Task.Run (() => Create (fileSource, token));

        public void Create(ITorrentFileSource fileSource, Stream stream)
            => Create (fileSource, stream, CancellationToken.None);

        public Task CreateAsync (ITorrentFileSource fileSource, Stream stream)
            => Task.Run (() => Create (fileSource, stream, CancellationToken.None));

        public Task CreateAsync (ITorrentFileSource fileSource, Stream stream, CancellationToken token)
            => Task.Run (() => Create (fileSource, stream, token));

        public void Create(ITorrentFileSource fileSource, string savePath)
            => Create (fileSource, savePath, CancellationToken.None);

        public Task CreateAsync (ITorrentFileSource fileSource, string savePath)
            => Task.Run (() => Create (fileSource, savePath, CancellationToken.None));

        public Task CreateAsync (ITorrentFileSource fileSource, string savePath, CancellationToken token)
            => Task.Run (() => Create (fileSource, savePath, token));

        void Create(ITorrentFileSource fileSource, Stream stream, CancellationToken token)
        {
            Check.Stream(stream);

            var data = Create(fileSource, token).Encode();
            stream.Write(data, 0, data.Length);
        }

        void Create(ITorrentFileSource fileSource, string savePath, CancellationToken token)
        {
            Check.SavePath(savePath);

            File.WriteAllBytes(savePath, Create(fileSource, token).Encode());
        }

        BEncodedDictionary Create (ITorrentFileSource fileSource, CancellationToken token)
        {
            Check.FileSource(fileSource);

            List <FileMapping> mappings = new List <FileMapping> (fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException ("The file source must contain one or more files", "fileSource");

            mappings.Sort((left, right) => left.Destination.CompareTo(right.Destination));
            Validate (mappings);

            List<TorrentFile> maps = new List <TorrentFile> ();
            foreach (FileMapping m in fileSource.Files)
                maps.Add (new TorrentFile (m.Destination, new FileInfo (m.Source).Length, m.Source));
            return Create(fileSource.TorrentName, maps, token);
        }

        internal BEncodedDictionary Create(string name, List<TorrentFile> files, CancellationToken token)
        {
            if (!InfoDict.ContainsKey (PieceLengthKey))
                PieceLength = RecommendedPieceSize(files);

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            BEncodedDictionary info = (BEncodedDictionary) torrent ["info"];

            info ["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            using (IPieceWriter reader = CreateReader ()) {
                info ["pieces"] = (BEncodedString) CalcPiecesHash (files, reader, token);

                if (files.Count == 1 && files [0].Path == name)
                    CreateSingleFileTorrent (torrent, files, reader, name);
                else
                    CreateMultiFileTorrent (torrent, files, reader, name);
            }

            return torrent;
        }

        void AddCommonStuff (BEncodedDictionary torrent)
        {
            if (Announces.Count == 0 || (Announces.Count == 1 && Announces [0].Count <= 1))
                RemoveCustom ("announce-list");

            if (Announces.Count > 0 && Announces [0].Count > 0)
                Announce = Announces [0] [0];

            if (GetrightHttpSeeds.Count > 0) {
                BEncodedList seedlist = new BEncodedList ();
                seedlist.AddRange (GetrightHttpSeeds.Select (s => (BEncodedString)s ));
                torrent ["url-list"] = seedlist;
            }

            TimeSpan span = DateTime.UtcNow - new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            torrent ["creation date"] = new BEncodedNumber ((long) span.TotalSeconds);
        }

        byte [] CalcPiecesHash (List<TorrentFile> files, IPieceWriter writer, CancellationToken token)
        {
            int bufferRead = 0;
            long fileRead = 0;
            long overallRead = 0;
            MD5 md5Hasher = null;

            var shaHasher = HashAlgoFactory.Create<SHA1> ();
            var torrentHashes = new List<byte> ();
            var overallTotal = files.Sum (t => t.Length);
            var buffer = new byte [PieceLength];

            if (StoreMD5)
                md5Hasher = HashAlgoFactory.Create<MD5> ();

            try {
                foreach (TorrentFile file in files) {
                    fileRead = 0;
                    md5Hasher?.Initialize ();

                    while (fileRead < file.Length) {
                        int toRead = (int) Math.Min (buffer.Length - bufferRead, file.Length - fileRead);
                        int read = writer.Read(file, fileRead, buffer, bufferRead, toRead);
                        if (read == 0)
                            throw new InvalidOperationException ("No data could be read from the file");

                        token.ThrowIfCancellationRequested ();

                        md5Hasher?.TransformBlock (buffer, bufferRead, read, buffer, bufferRead);
                        shaHasher.TransformBlock (buffer, bufferRead, read, buffer, bufferRead);

                        bufferRead += read;
                        fileRead += read;
                        overallRead += read;

                        if (bufferRead == buffer.Length) {
                            bufferRead = 0;
                            shaHasher.TransformFinalBlock (buffer, 0, 0);
                            torrentHashes.AddRange (shaHasher.Hash);
                            shaHasher.Initialize();
                        }
                        Hashed?.InvokeAsync (this, new TorrentCreatorEventArgs (file.Path, fileRead, file.Length, overallRead, overallTotal));
                    }

                    md5Hasher?.TransformFinalBlock (buffer, 0, 0);
                    md5Hasher?.Initialize ();
                    file.MD5 = md5Hasher?.Hash;
                }
                if (bufferRead > 0) {
                    shaHasher.TransformFinalBlock (buffer, 0, 0);
                    torrentHashes.AddRange (shaHasher.Hash);
                }
            } finally {
                shaHasher.Dispose ();
                md5Hasher?.Dispose ();
            }
            return torrentHashes.ToArray ();
        }

        void CreateMultiFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, IPieceWriter writer, string name)
        {
            BEncodedDictionary info = (BEncodedDictionary) dictionary ["info"];
            List<BEncodedValue> files = mappings.ConvertAll<BEncodedValue> (ToFileInfoDict);
            info.Add ("files", new BEncodedList (files));
        }

        protected virtual IPieceWriter CreateReader ()
        {
            return new DiskWriter ();
        }

        void CreateSingleFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, IPieceWriter writer, string name)
        {
            BEncodedDictionary infoDict = (BEncodedDictionary) dictionary ["info"];
            infoDict.Add ("length", new BEncodedNumber (mappings [0].Length));
            if (mappings [0].MD5 != null)
                infoDict ["md5sum"] = (BEncodedString) mappings [0].MD5;
        }

        static BEncodedValue ToFileInfoDict (TorrentFile file)
        {
            BEncodedDictionary fileDict = new BEncodedDictionary ();

            BEncodedList filePath = new BEncodedList ();
            string [] splittetPath = file.Path.Split (new char [] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in splittetPath)
                filePath.Add (new BEncodedString (s));

            fileDict ["length"] = new BEncodedNumber (file.Length);
            fileDict ["path"] = filePath;
            if (file.MD5 != null)
                fileDict ["md5sum"] = (BEncodedString) file.MD5;

            return fileDict;
        }

        static void Validate (List <FileMapping> maps)
        {
            // Make sure the user doesn't try to overwrite system files. Ensure
            // that the path is relative and doesn't try to access its parent folder
            var sepLinux = "/";
            var sepWindows = "\\";
            var dropLinux = "../";
            var dropWindows = "..\\";
            foreach (var map in maps) {
                if (map.Destination.StartsWith (sepLinux))
                    throw new ArgumentException ("The destination path cannot start with the '{0}' character", sepLinux);
                if (map.Destination.StartsWith (sepWindows))
                    throw new ArgumentException ("The destination path cannot start with the '{0}' character", sepWindows);

                if (map.Destination.Contains (dropLinux))
                    throw new ArgumentException ("The destination path cannot contain '{0}'", dropLinux);
                if (map.Destination.Contains (dropWindows))
                    throw new ArgumentException ("The destination path cannot contain '{0}'", dropWindows);
            }

            // Ensure all the destination files are unique too. The files should already be sorted.
            for (int i = 1; i < maps.Count; i++)
                if (maps[i - 1].Destination == maps [i].Destination)
                    throw new ArgumentException (string.Format ("Files '{0}' and '{1}' both map to the same destination '{2}'",
                                                 maps [i - 1].Source,
                                                 maps [i].Source,
                                                 maps [i].Destination));
        }
    }
}
