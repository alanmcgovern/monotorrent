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
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using MonoTorrent.BEncoding;
using System.Threading;
using MonoTorrent.Client;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent.Common {

    public class TorrentCreator {

        static BEncodedValue Get (BEncodedDictionary dictionary, BEncodedString key)
        {
            return dictionary.ContainsKey (key) ? dictionary [key] : null;
        }

        public static IEnumerable<FileMapping> GetFileMappings (string path)
        {
            return GetAllMappings (path, true);
        }
        public static IEnumerable<FileMapping> GetFileMappings (string path, bool ignoreHiddenFiles)
        {
            return GetAllMappings (path, ignoreHiddenFiles);
        }

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


        List<List<string>> announces;
        TorrentCreatorAsyncResult asyncResult;
        BEncodedDictionary dict;
        List<string> getrightHttpSeeds;
        bool ignoreHiddenFiles;
        BEncodedDictionary info;
        bool storeMD5;


        public List<List<string>> Announces
        {
            get { return this.announces; }
        }

        public string Comment
        {
            get
            {
                BEncodedValue val = Get (this.dict, new BEncodedString ("comment"));
                return val == null ? string.Empty : val.ToString ();
            }
            set { dict ["comment"] = new BEncodedString (value); }
        }

        public string CreatedBy
        {
            get
            {
                BEncodedValue val = Get (this.dict, new BEncodedString ("created by"));
                return val == null ? string.Empty : val.ToString ();
            }
            set { dict ["created by"] = new BEncodedString (value); }
        }

        public string Encoding
        {
            get { return Get (this.dict, (BEncodedString) "encoding").ToString (); }
            private set { dict ["encoding"] = (BEncodedString) value; }
        }

        public List<string> GetrightHttpSeeds
        {
            get { return getrightHttpSeeds; }
        }

        public bool IgnoreHiddenFiles
        {
            get { return ignoreHiddenFiles; }
            set { ignoreHiddenFiles = value; }
        }

        public bool StoreMD5
        {
            get { return storeMD5; }
            set { storeMD5 = value; }
        }

        public long? PieceLength
        {
            get
            {
                BEncodedValue val = Get (info, new BEncodedString ("piece length"));
                return val == null ? (long?)null : ((BEncodedNumber) val).Number;
            }
            set {
                if (value.HasValue)
                    info ["piece length"] = new BEncodedNumber (value.Value);
                else
                    this.info.Remove ("piece length");
            }
        }

        public bool Private
        {
            get
            {
                BEncodedValue val = Get (info, new BEncodedString ("private"));
                return val == null ? false : ((BEncodedNumber) val).Number == 1;
            }
            set { info ["private"] = new BEncodedNumber (value ? 1 : 0); }
        }

        public string Publisher
        {
            get
            {
                BEncodedValue val = Get (info, new BEncodedString ("publisher"));
                return val == null ? string.Empty : val.ToString ();
            }
            set { info ["publisher"] = new BEncodedString (value); }
        }

        public string PublisherUrl
        {
            get
            {
                BEncodedValue val = Get (info, new BEncodedString ("publisher-url"));
                return val == null ? string.Empty : val.ToString ();
            }
            set { info ["publisher-url"] = new BEncodedString (value); }
        }


        public TorrentCreator ()
        {
            announces = new List<List<string>> ();
            dict = new BEncodedDictionary ();
            getrightHttpSeeds = new List<string> ();
            ignoreHiddenFiles = true;
            info = new BEncodedDictionary ();

            CreatedBy = string.Format ("MonoTorrent {0}", VersionInfo.Version);
            Encoding = "UTF-8";
        }


        public void AbortCreation ()
        {
            TorrentCreatorAsyncResult r = asyncResult;
            if (r != null)
                r.Aborted = true;
        }

        void AddCommonStuff (BEncodedDictionary torrent)
        {
            if (announces.Count > 0 && announces [0].Count > 0)
                torrent.Add ("announce", new BEncodedString (announces [0] [0]));

            // If there is more than one tier or the first tier has more than 1 tracker
            if (announces.Count > 1 || (announces.Count > 0 && announces [0].Count > 1)) {
                BEncodedList announceList = new BEncodedList ();
                for (int i = 0; i < this.announces.Count; i++) {
                    BEncodedList tier = new BEncodedList ();
                    for (int j = 0; j < this.announces [i].Count; j++)
                        tier.Add (new BEncodedString (this.announces [i] [j]));

                    announceList.Add (tier);
                }

                torrent.Add ("announce-list", announceList);
            }

            if (getrightHttpSeeds.Count > 0) {
                BEncodedList seedlist = new BEncodedList ();
                seedlist.AddRange (getrightHttpSeeds.ConvertAll<BEncodedValue> (delegate (string s) { return (BEncodedString) s; }));
                torrent ["url-list"] = seedlist;
            }

            TimeSpan span = DateTime.Now - new DateTime (1970, 1, 1);
            torrent ["creation date"] = new BEncodedNumber ((long) span.TotalSeconds);
        }

        public void AddCustom (BEncodedString key, BEncodedValue value)
        {
            Check.Key (key);
            Check.Value (value);
            dict [key] = value;
        }

        public void AddCustomSecure (BEncodedString key, BEncodedValue value)
        {
            Check.Key (key);
            Check.Value (value);
            info [key] = value;
        }

        public IAsyncResult BeginCreate (string path, AsyncCallback callback, object asyncState)
        {
            return BeginCreate (delegate { return Create (path); }, callback, asyncState);
        }

        public IAsyncResult BeginCreate (string directory, IEnumerable<string> fullPaths, AsyncCallback callback, object asyncState)
        {
            return BeginCreate (delegate { return Create (directory, fullPaths); }, callback, asyncState);
        }

        public IAsyncResult BeginCreate (string name, IEnumerable<FileMapping> files, AsyncCallback callback, object asyncState)
        {
            return BeginCreate (delegate { return Create (name, files); }, callback, asyncState);
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

            long pieceLength = PieceLength.Value;
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
                        RaiseHashed (new TorrentCreatorEventArgs (fileRead, file.Length, overallRead, overallTotal));
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

        public BEncodedDictionary Create (string path)
        {
            Check.Path (path);
            path = Path.GetFullPath (path);

            List<FileMapping> mappings = GetAllMappings (path, IgnoreHiddenFiles);
            string name = mappings.Count == 1 ? Path.GetFileName (mappings [0].Destination) : new DirectoryInfo (path).Name;
            return Create (name, mappings);
        }

        public BEncodedDictionary Create (string directory, IEnumerable<string> fullPaths)
        {
            List<FileMapping> mappings = new List<FileMapping> ();
            foreach (string s in fullPaths)
                mappings.Add (new FileMapping (s, s.Substring (directory.Length + 1)));

            string name = mappings.Count == 1 ? Path.GetFileName (mappings [0].Destination) : new DirectoryInfo (directory).Name;
            return Create (name, mappings);
        }

        public BEncodedDictionary Create (string name, IEnumerable<FileMapping> mappings)
        {
            Check.Name (name);
            Check.Mappings (mappings);

            List<TorrentFile> maps = new List<TorrentFile> ();
            foreach (FileMapping m in mappings)
                maps.Add (ToTorrentFile (m));

            if (maps.Count == 0)
                throw new ArgumentException ("Path must refer to a file or a directory containing one or more files", "path");
           
            return Create(name, maps);
        }

        internal BEncodedDictionary Create(string name, List<TorrentFile> files)
        {
            if (!PieceLength.HasValue)
                PieceLength = RecommendedPieceSize(files);

            BEncodedDictionary torrent = BEncodedDictionary.Decode<BEncodedDictionary> (dict.Encode ());
            BEncodedDictionary info = BEncodedDictionary.Decode<BEncodedDictionary> (this.info.Encode ());
            torrent ["info"] = info;

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

        public bool EndCreate (IAsyncResult result, string path)
        {
            if (string.IsNullOrEmpty (path))
                throw new ArgumentNullException ("path");

            using (FileStream s = File.OpenWrite (path))
                return EndCreate (result, s);
        }

        public bool EndCreate (IAsyncResult result, Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException ("stream");

            BEncodedDictionary data = EndCreate (result);
            byte [] buffer = data.Encode ();
            if (data != null)
                stream.Write (buffer, 0, buffer.Length);

            return data != null;
        }

        static List<FileMapping> GetAllMappings (string path, bool ignoreHiddenFiles)
        {
            if (File.Exists (path)) {
                List<FileMapping> mappings = new List<FileMapping> ();
                mappings.Add (new FileMapping (path, Path.GetFileName (path)));
                return mappings;
            }

            List<string> files = new List<string> ();
            Queue<string> directories = new Queue<string> ();
            directories.Enqueue (path);

            while (directories.Count > 0) {
                string current = directories.Dequeue ();
                if (ignoreHiddenFiles) {
                    DirectoryInfo info = new DirectoryInfo (current);
                    if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                }

                foreach (string s in Directory.GetDirectories (current))
                    directories.Enqueue (s);

                files.AddRange (Directory.GetFiles (current));
            }

            if (ignoreHiddenFiles) {
                files.RemoveAll (delegate (string file) {
                    return (new FileInfo (file).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                });
            }

            files.Sort (StringComparer.Ordinal);
            return files.ConvertAll<FileMapping> (delegate (string file) {
                return new FileMapping (file, file.Substring (path.Length + 1));
            });
        }

        public void RemoveCustom (BEncodedString key)
        {
            Check.Key (key);
            dict.Remove (key);
        }

        public void RemoveCustomSecure (BEncodedString key)
        {
            Check.Key (key);
            info.Remove (key);
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
    }
}
