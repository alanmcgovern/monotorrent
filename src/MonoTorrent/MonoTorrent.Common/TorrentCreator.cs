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
using System.Threading;

using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;
using System.Security.Cryptography;

namespace MonoTorrent.Common
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
        {
            long total = 0;
            foreach (string file in files)
                total += new FileInfo (file).Length;
            return RecommendedPieceSize (total);
        }
        public static int RecommendedPieceSize (IEnumerable<TorrentFile> files)
        {
            long total = 0;
            foreach (TorrentFile file in files)
                total += file.Length;
            return RecommendedPieceSize (total);
        }
        public static int RecommendedPieceSize (IEnumerable<FileMapping> files)
        {
            long total = 0;
            foreach (FileMapping file in files)
                total += new FileInfo (file.Source).Length;
            return RecommendedPieceSize (total);
        }


        public event EventHandler<TorrentCreatorEventArgs> Hashed;


        TorrentCreatorAsyncResult asyncResult;
        List<string> getrightHttpSeeds;
        bool storeMD5;

        public List<string> GetrightHttpSeeds
        {
            get { return getrightHttpSeeds; }
        }

        public bool StoreMD5
        {
            get { return storeMD5; }
            set { storeMD5 = value; }
        }

        public TorrentCreator ()
        {
            getrightHttpSeeds = new List<string> ();
            CanEditSecureMetadata = true;
            CreatedBy = string.Format ("MonoTorrent {0}", VersionInfo.Version);
        }


        public void AbortCreation ()
        {
            TorrentCreatorAsyncResult r = asyncResult;
            if (r != null)
                r.Aborted = true;
        }

        void AddCommonStuff (BEncodedDictionary torrent)
        {
            if (Announces.Count > 0 && Announces [0].Count > 0)
                Announce = Announces [0] [0];

            if (getrightHttpSeeds.Count > 0) {
                BEncodedList seedlist = new BEncodedList ();
                seedlist.AddRange (getrightHttpSeeds.ConvertAll<BEncodedValue> (delegate (string s) { return (BEncodedString) s; }));
                torrent ["url-list"] = seedlist;
            }

            TimeSpan span = DateTime.Now - new DateTime (1970, 1, 1);
            torrent ["creation date"] = new BEncodedNumber ((long) span.TotalSeconds);
        }

        public IAsyncResult BeginCreate (ITorrentFileSource fileSource, AsyncCallback callback, object asyncState)
        {
            return BeginCreate(delegate { return Create(fileSource); }, callback, asyncState);
        }

        IAsyncResult BeginCreate (MainLoopJob task, AsyncCallback callback, object asyncState)
        {
            if (asyncResult != null)
                throw new InvalidOperationException ("Two asynchronous operations cannot be executed simultaenously");

            asyncResult = new TorrentCreatorAsyncResult (callback, asyncState);
            ThreadPool.QueueUserWorkItem (delegate {
                try {
                    asyncResult.Dictionary = (BEncodedDictionary) task ();
                } catch (Exception ex) {
                    asyncResult.SavedException = ex;
                }
                asyncResult.Complete ();
            });
            return asyncResult;
        }

        byte [] CalcPiecesHash (List<TorrentFile> files, PieceWriter writer)
        {
            byte [] buffer = null;
            int bufferRead = 0;
            long fileRead = 0;
            long overallRead = 0;
            long overallTotal = 0;
            MD5 md5Hasher = null;
            SHA1 shaHasher = null;
            List<byte> torrentHashes = null;

            shaHasher = HashAlgoFactory.Create<SHA1> ();
            torrentHashes = new List<byte> ();
            overallTotal = Toolbox.Accumulate<TorrentFile> (files, delegate (TorrentFile m) { return m.Length; });

            long pieceLength = PieceLength;
            buffer = new byte [pieceLength];

            if (StoreMD5)
                md5Hasher = HashAlgoFactory.Create<MD5> ();

            try {
                foreach (TorrentFile file in files) {
                    fileRead = 0;
                    if (md5Hasher != null)
                        md5Hasher.Initialize ();

                    while (fileRead < file.Length) {


                        int toRead = (int) Math.Min (buffer.Length - bufferRead, file.Length - fileRead);
                        int read = writer.Read(file, fileRead, buffer, bufferRead, toRead);

                        if (md5Hasher != null)
                            md5Hasher.TransformBlock (buffer, bufferRead, read, buffer, bufferRead);
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
                        RaiseHashed (new TorrentCreatorEventArgs (file.Path, fileRead, file.Length, overallRead, overallTotal));
                    }
                    if (md5Hasher != null) {
                        md5Hasher.TransformFinalBlock (buffer, 0, 0);
                        md5Hasher.Initialize ();
                        file.MD5 = md5Hasher.Hash;
                    }
                }
                if (bufferRead > 0) {
                    shaHasher.TransformFinalBlock (buffer, 0, 0);
                    torrentHashes.AddRange (shaHasher.Hash);
                }
            } finally {
                if (shaHasher != null)
                    shaHasher.Clear ();
                if (md5Hasher != null)
                    md5Hasher.Clear ();
            }
            return torrentHashes.ToArray ();
        }

        public BEncodedDictionary Create (ITorrentFileSource fileSource)
        {
            Check.FileSource(fileSource);

            List <FileMapping> mappings = new List <FileMapping> (fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException ("The file source must contain one or more files", "fileSource");

            mappings.Sort((left, right) => left.Destination.CompareTo(right.Destination));
            Validate (mappings);

            List<TorrentFile> maps = new List <TorrentFile> ();
            foreach (FileMapping m in fileSource.Files)
                maps.Add (ToTorrentFile (m));
            return Create(fileSource.TorrentName, maps);
        }

        public void Create(ITorrentFileSource fileSource, Stream stream)
        {
            Check.Stream(stream);

            var data = Create(fileSource).Encode();
            stream.Write(data, 0, data.Length);
        }

        public void Create(ITorrentFileSource fileSource, string savePath)
        {
            Check.SavePath(savePath);

            File.WriteAllBytes(savePath, Create(fileSource).Encode());
        }

        internal BEncodedDictionary Create(string name, List<TorrentFile> files)
        {
            if (PieceLength == 0)
                PieceLength = RecommendedPieceSize(files);

            BEncodedDictionary torrent = BEncodedValue.Clone (Metadata);
            BEncodedDictionary info = (BEncodedDictionary) torrent ["info"];

            info ["name"] = (BEncodedString) name;
            AddCommonStuff (torrent);

            using (PieceWriter reader = CreateReader ()) {
                info ["pieces"] = (BEncodedString) CalcPiecesHash (files, reader);

                if (files.Count == 1 && files [0].Path == name)
                    CreateSingleFileTorrent (torrent, files, reader, name);
                else
                    CreateMultiFileTorrent (torrent, files, reader, name);
            }

            return torrent;
        }

        void CreateMultiFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, PieceWriter writer, string name)
        {
            BEncodedDictionary info = (BEncodedDictionary) dictionary ["info"];
            List<BEncodedValue> files = mappings.ConvertAll<BEncodedValue> (ToFileInfoDict);
            info.Add ("files", new BEncodedList (files));
        }

        protected virtual PieceWriter CreateReader ()
        {
            return new DiskWriter ();
        }

        void CreateSingleFileTorrent (BEncodedDictionary dictionary, List<TorrentFile> mappings, PieceWriter writer, string name)
        {
            BEncodedDictionary infoDict = (BEncodedDictionary) dictionary ["info"];
            infoDict.Add ("length", new BEncodedNumber (mappings [0].Length));
            if (mappings [0].MD5 != null)
                infoDict ["md5sum"] = (BEncodedString) mappings [0].MD5;
        }

        public BEncodedDictionary EndCreate (IAsyncResult result)
        {
            Check.Result (result);

            if (result != this.asyncResult)
                throw new ArgumentException ("The supplied async result does not correspond to currently active async result");

            try {
                if (!result.IsCompleted)
                    result.AsyncWaitHandle.WaitOne ();

                if (this.asyncResult.SavedException != null)
                    throw this.asyncResult.SavedException;

                return this.asyncResult.Aborted ? null : this.asyncResult.Dictionary;
            } finally {
                this.asyncResult = null;
            }
        }

        public void EndCreate (IAsyncResult result, string path)
        {
            Check.PathNotEmpty(path);

            var dict = EndCreate(result);
            File.WriteAllBytes(path, dict.Encode());
        }

        public void EndCreate(IAsyncResult result, Stream stream)
        {
            Check.Stream(stream);

            var buffer = EndCreate (result).Encode ();
            stream.Write (buffer, 0, buffer.Length);
        }

        void RaiseHashed (TorrentCreatorEventArgs e)
        {
            Toolbox.RaiseAsyncEvent<TorrentCreatorEventArgs> (Hashed, this, e);
        }

        TorrentFile ToTorrentFile (FileMapping mapping)
        {
            FileInfo info = new FileInfo (mapping.Source);
            return new TorrentFile (mapping.Destination, info.Length, mapping.Source);
        }

        BEncodedValue ToFileInfoDict (TorrentFile file)
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

        void Validate (List <FileMapping> maps)
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
