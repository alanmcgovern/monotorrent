//
// TorrentCreator.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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



namespace MonoTorrent.Common
{
    ///<summary>
    ///this class is used to create on disk torrent files.
    ///</summary>
    public class TorrentCreator
    {
        private List<string> announces;//list of announce urls
        private bool doDummy;//used for size calculating


        public TorrentCreator()
        {
            announces = new List<string>();
        }

        ///<summary>the path from which you would like to make a torrent of.
        ///path can be a file and a directory. 
        ///</summary>
        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }
        private string path;

        ///<summary>
        ///this property sets the length of the pieces. default is 512 kb.
        ///</summary>
        public int PieceLength
        {
            get
            {
                return piece_length;
            }
            set
            {
                piece_length = value;
            }
        }
        private int piece_length = 2 << 18;

        ///<summary>
        ///this property sets the comment entry in the torrent file. default is string.Empty to conserve bandwidth.
        ///</summary>
        public string Comment
        {
            get
            {
                return comment;
            }
            set
            {
                comment = value;
            }
        }
        private string comment = string.Empty;

        ///<summary>this property sets the created by entrie in the torrent file
        ///</summary>
        public string CreatedBy
        {
            get
            {
                return created_by;
            }
            set
            {
                created_by = value;
            }
        }
        private string created_by = string.Empty;

        ///<summary>this property sets wheather the optional field creation date should be included
        ///in the torrent or not. default is false to conserve bandwidth. </summary>
        public bool StoreCreationDate
        {
            get
            {
                return store_date;
            }
            set
            {
                store_date = value;
            }
        }
        private bool store_date = false;

        ///<summary>this property sets wheather the optional field md5sum should be included. 
        ///default is false to conserve bandwidth
        ///</summary>
        public bool StoreMD5
        {
            get
            {
                return store_md5;
            }
            set
            {
                store_md5 = value;
            }
        }
        private bool store_md5 = false;


        ///<summary>
        ///this property instructs the creator to use add utf8 strings
        ///default value is false to conserve bandwidth.
        ///</summary>
        public bool StoreUTF8
        {
            get
            {
                return store_utf8;
            }
            set
            {
                store_utf8 = value;
            }
        }
        private bool store_utf8 = false;

        public bool IgnoreDotFiles
        {
            get
            {
                return ignore_dot_files;
            }
            set
            {
                ignore_dot_files = value;
            }
        }
        private bool ignore_dot_files = true;

        ///<summary>
        ///returns the size of bytes required by the torrents with the current set properties.        
        ///</summary>
        public int GetSize()
        {
            doDummy = true; //do not calc sha1 and md5 takes too long
            int length = CreateDict().Encode().Length;
            doDummy = false;
            return length;
        }

        ///<summary>
        ///this method adds an announce url to the list of announce urls.
        ///if the list contains more than one entries the announce list is used
        ///to store the list of announce urls.
        ///</summary>
        public void AddAnnounce(string url)
        {
            announces.Add(url);
        }

        ///<summary>
        ///removes an url from the list
        ///</summary>
        public void RemoveAnnounce(string url)
        {
            announces.Remove(url);
        }
        
        /// <summary>
        /// This can be used to add Custom Values to the Torrent. This may be used to add" httpseeds" 
        /// to the Torrent.
        /// </summary>        
        public void AddCustom(KeyValuePair<BEncodedString, IBEncodedValue> customInfo)
        {
        	custom_info = customInfo;
        	use_custom = true;
        }
        private KeyValuePair<BEncodedString, IBEncodedValue> custom_info;
        private bool use_custom = false;
        
        /// <summary>This removes the Custom Values from the Torrent.
        /// </summary>
        public void RemoveCustom()
        {
        	use_custom = false;
        }

        ///<summary>
        ///creates and stores a torrent at storagePath
        ///<summary>
        ///<param name="storagePath">place and name to store the torrent file</param>
        public void Create(string storagePath)
        {
            doDummy = false;
            BEncodedDictionary torrentDict = CreateDict();

            using (FileStream stream = new FileStream(storagePath, FileMode.Create))
            {
                byte[] data = torrentDict.Encode();
                stream.Write(data, 0, data.Length);
            }
        }

        ///<summary>
        ///creates an BencodedDictionary.
        ///</summary>
        ///<returns>a BDictionary representing the torrent</returns>
        private BEncodedDictionary CreateDict()
        {
            if (Directory.Exists(Path))
            {
                Debug.WriteLine("creating multi torrent from " + Path);
                return CreateMultiFileTorrent();
            }
            if (File.Exists(Path))
            {
                Debug.WriteLine("creating single torrent from " + Path);
                return CreateSingleFileTorrent();
            }

            throw new ArgumentException("no such file or directory", "storagePath");
        }

        ///<summary>
        ///used for creating multi file mode torrents.
        ///</summary>
        ///<returns>the dictionary representing which is stored in the torrent file</returns>
        protected BEncodedDictionary CreateMultiFileTorrent()
        {
            BEncodedDictionary bencodedDict = new BEncodedDictionary();
            AddCommonStuff(bencodedDict);

            BEncodedDictionary info = new BEncodedDictionary();

            List<string> fullPaths = new List<string>();//store files to hash over
            BEncodedList files = new BEncodedList();//the dict which hold the file infos


            AddAllFileInfoDicts(Path, files, fullPaths);//do recursively

            info.Add("files", files);

            string name = GetDirName(Path);

            Debug.WriteLine("topmost dir: " + name);
            AddString(info, "name", name);

            info.Add("piece length", new BEncodedNumber(PieceLength));

            info.Add("pieces", new BEncodedString(CalcPiecesHash(fullPaths)));

            bencodedDict.Add("info", info);

            return bencodedDict;
        }

        ///<summary>
        ///used for creating a single file torrent file
        ///<summary>
        ///<returns>the dictionary representing which is stored in the torrent file</returns>
        protected BEncodedDictionary CreateSingleFileTorrent()
        {
            BEncodedDictionary torrentDict = new BEncodedDictionary();
            AddCommonStuff(torrentDict);

            BEncodedDictionary infoDict = new BEncodedDictionary();

            infoDict.Add("length", new BEncodedNumber(new FileInfo(Path).Length));
            if (StoreMD5)
            {
                AddMD5(infoDict, Path);
            }

            AddString(infoDict, "name", System.IO.Path.GetFileName(Path));
            Console.WriteLine("name == " + System.IO.Path.GetFileName(Path));
            infoDict.Add("piece length", new BEncodedNumber(PieceLength));
            List<string> files = new List<string>();
            files.Add(Path);
            infoDict.Add("pieces", new BEncodedString(CalcPiecesHash(files)));

            torrentDict.Add("info", infoDict);

            return torrentDict;
        }


        ///<summary>
        ///this method runs recursively through all subdirs under dir and ads information
        ///from each file to the filesList. 
        ///<summary>
        ///<param name="dir">the top directory to start from</param>
        ///<param name="filesList">the list to store the file information</param>
        ///<param name="paths">a list of all files found. used for piece hashing later</param>
        private void AddAllFileInfoDicts(string dir, BEncodedList filesList, List<string> paths)
        {
            string[] subs = Directory.GetFileSystemEntries(dir);
            foreach (string path in subs)
            {
                if (Directory.Exists(path))
                {
                    if (ignore_dot_files && System.IO.Path.GetDirectoryName(path)[0] == '.')
                        continue;

                    AddAllFileInfoDicts(path, filesList, paths);
                    continue;
                }

                if (ignore_dot_files && System.IO.Path.GetFileName(path)[0] == '.')
                    continue;

                filesList.Add(GetFileInfoDict(path, dir));
                paths.Add(path);
            }
        }

        ///<summary>
        ///this method is used for multi file mode torrents to return a dictionary with
        ///file relevant informations. 
        ///<param name="file">the file to report the informations for</param>
        ///<param name="basePath">used to subtract the absolut path information</param>
        ///</summary>
        private BEncodedDictionary GetFileInfoDict(string file, string basePath)
        {
            BEncodedDictionary fileDict = new BEncodedDictionary();

            fileDict.Add("length", new BEncodedNumber(new FileInfo(file).Length));

            if (StoreMD5)
            {
                AddMD5(fileDict, file);
            }

            file = file.Remove(0, Path.Length);
            Debug.WriteLine("ohne base[" + basePath + "] " + file);
            BEncodedList path = new BEncodedList();
            string[] splittetPath = file.Split(System.IO.Path.DirectorySeparatorChar);

            foreach (string s in splittetPath)
            {
                if (s.Length > 0)//exclude empties
                    path.Add(new BEncodedString(s));
            }

            fileDict.Add("path", path);

            return fileDict;
        }

        ///<summary>
        ///this adds stuff common to single and multi file torrents
        ///</summary>
        private void AddCommonStuff(BEncodedDictionary torrent)
        {
            Debug.WriteLine(announces[0]);
            torrent.Add("announce", new BEncodedString(announces[0]));

            if (announces.Count > 1) {
                BEncodedList announceList = new BEncodedList();
                foreach (string announce in announces)
                {
                    announceList.Add(new BEncodedString(announce));
                }
                torrent.Add("announce-list", announceList);
            }

            if (Comment.Length > 0) {
                AddString(torrent, "comment", Comment);
            }

            if (CreatedBy.Length > 0) {
                AddString(torrent, "created by", CreatedBy);
            }

            if (StoreCreationDate) {
                DateTime epocheStart = new DateTime(1970, 1, 1);
                TimeSpan span = DateTime.Now - epocheStart;
                Debug.WriteLine("creation date: " + DateTime.Now.ToString() + " - " + epocheStart.ToString() + " = " + span.ToString() + " : " + (long)span.TotalSeconds);
                torrent.Add("creation date", new BEncodedNumber((long)span.TotalSeconds));
            }			

            torrent.Add("encoding", (BEncodedString)"UTF-8");
            
            if (use_custom) {
            	torrent.Add(custom_info);
            }
        }

        ///<summary>calculate md5sum of a file</summary>
        ///<param name="fileName">the file to sum with md5</param>
        private void AddMD5(BEncodedDictionary dict, string fileName)
        {
            MD5 hasher = MD5.Create();
            StringBuilder sb = new StringBuilder();
            if (doDummy)
            {
                dict.Add("md5sum", new BEncodedString("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"));
                return;
            }
            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            {
                byte[] hash = hasher.ComputeHash(stream);

                foreach (byte b in hash)
                {
                    string hex = b.ToString("X");
                    hex = hex.Length > 1 ? hex : "0" + hex;
                    sb.Append(hex);
                }
                Console.WriteLine("sum for file " + fileName + " = " + sb.ToString());
            }
            dict.Add("md5sum", new BEncodedString(sb.ToString()));
        }

        ///<summary>
        ///calculates all hashes over the files which should be included in the torrent
        ///</summmary>
        private byte[] CalcPiecesHash(List<string> fullPaths)
        {
            SHA1 hasher = SHA1.Create();
            byte[] piece = new byte[PieceLength];//holds one piece for sha1 calcing
            int len = 0;        //holds the bytes read by the stream.Read() method
            byte[] piecesBuffer = new byte[GetPieceCount(fullPaths) * 20]; //holds all the pieces hashes
            int piecesBufferOffset = 0;
            if (doDummy)
            {
                return piecesBuffer;
            }
            using (CatStreamReader reader = new CatStreamReader(fullPaths))
            {
                //go through each path added earlier by AddAllInfoDicts
                len = reader.Read(piece, 0, piece.Length);
                while (len != 0)
                {
                    byte[] currentHash = hasher.ComputeHash(piece, 0, len);
                    Debug.Assert(currentHash.Length == 20);
                    Array.Copy(currentHash, 0, piecesBuffer, piecesBufferOffset, currentHash.Length);
                    piecesBufferOffset += currentHash.Length;
                    len = reader.Read(piece, 0, piece.Length);
                }
            }
            return piecesBuffer;
        }

        private int GetPieceCount(List<string> fullPaths)
        {
            long size = 0;
            foreach (string file in fullPaths)
            {
                size += new FileInfo(file).Length;
            }
            //double count = (double)size/PieceLength;
            Console.WriteLine("piece count: " + Math.Ceiling((double)size / PieceLength));
            return (int)Math.Ceiling((double)size / PieceLength);
        }

        ///<summary>
        ///adds a string to a dictionary and checks to also include key-utf8 if needed
        ///<summary>
        ///<param name="dict">the dictionary to add the string to</param>
        ///<param name="key">the key for the string</param>
        ///<param name="str">the string</param>
        private void AddString(BEncodedDictionary dict, string key, string str)
        {
            //Console.WriteLine(key + " = " + str );
            dict.Add(key, new BEncodedString(str));
            if (StoreUTF8)
            {
                dict.Add(key + ".utf-8", new BEncodedString(Encoding.UTF8.GetBytes(str)));
            }
        }

        private string GetDirName(string path)
        {
            string[] pathEntries = path.Split(new char[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            int i = pathEntries.Length - 1;
            while (String.IsNullOrEmpty(pathEntries[i]))
                i--;
            return pathEntries[i];
        }
    }

    ///<summary>
    ///this class is used to concatenate all the files provided as parameter from the constructor.
    ///<summary>
    internal class CatStreamReader : IDisposable
    {
        private List<string> _files;
        private int _currentFile = 0;
        private FileStream _currentStream = null;

        public CatStreamReader(List<string> files)
        {
            _files = files;
            _currentStream = new FileStream(files[_currentFile++], FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public int Read(byte[] data, int offset, int count)
        {
            int len = 0;

            while (len != count)
            {
                int tmp = _currentStream.Read(data, offset + len, count - len);
                len += tmp;
                if (tmp == 0)
                {
                    if (_currentFile < _files.Count)
                    {
                        _currentStream.Close();
                        _currentStream.Dispose();
                        _currentStream = new FileStream(_files[_currentFile++], FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    else
                    {
                        return len;
                    }
                }
            }
            return len;
        }


        public void Dispose()
        {
            _currentStream.Close();
            _currentStream.Dispose();
        }
    }
}