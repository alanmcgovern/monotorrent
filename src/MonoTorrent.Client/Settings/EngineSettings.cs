//
// EngineSettings.cs
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



using MonoTorrent.Common;
using System.Reflection;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    public class EngineSettings
    {
        #region Member Variables
        /// <summary>
        /// This is the default directory that torrents will be downloaded to
        /// </summary>
        public string SavePath
        {
            get { return this.savePath; }
            set { this.savePath = value; }
        }
        private string savePath;


        /// <summary>
        /// This is the combined maximum open connections for all running torrents
        /// </summary>
        public int GlobalMaxConnections
        {
            get { return this.globalMaxConnections; }
            set { this.globalMaxConnections = value; }
        }
        private int globalMaxConnections;


        /// <summary>
        /// This is the maximum half-open connections allowed
        /// </summary>
        public int GlobalMaxHalfOpenConnections
        {
            get { return this.globalMaxHalfOpenConnections; }
            set { this.globalMaxHalfOpenConnections = value; }
        }
        private int globalMaxHalfOpenConnections;


        /// <summary>
        /// This is the combined maximum download speed for all running torrents
        /// </summary>
        public int GlobalMaxDownloadSpeed
        {
            get { return this.globalMaxDownloadSpeed; }
            set { this.globalMaxDownloadSpeed = value; }
        }
        private int globalMaxDownloadSpeed;


        /// <summary>
        /// This is the combined maximum upload speed for all running torrents
        /// </summary>
        public int GlobalMaxUploadSpeed
        {
            get { return this.globalMaxUploadSpeed; }
            set { this.globalMaxUploadSpeed = value; }
        }
        private int globalMaxUploadSpeed;


        /// <summary>
        /// The port to listen for incoming connections on
        /// </summary>
        public int ListenPort
        {
            get { return this.listenPort; }
            set { this.listenPort = value; }
        }
        private int listenPort;
        #endregion


        #region Defaults
        private const string DefaultSavePath = "";
        private const int DefaultMaxConnections = 150;
        private const int DefaultMaxDownloadSpeed = 0;
        private const int DefaultMaxUploadSpeed = 0;
        private const int DefaultMaxHalfOpenConnections = 5;
        private const int DefaultListenPort = 52138;
        #endregion


        #region Constructors
        private EngineSettings()
        {
        }

        /// <summary>
        /// Initialises a new engine settings with the supplied values
        /// </summary>
        /// <param name="globalMaxConnections">The overall maximum number of open connections allowed</param>
        /// <param name="globalHalfOpenConnections">The overall maximum number of half open connections</param>
        /// <param name="defaultSavePath">The default path to save downloaded material to</param>
        /// <param name="listenPort">The port to listen for incoming connections on</param>
        public EngineSettings(int globalMaxConnections, int globalHalfOpenConnections, string defaultSavePath, int listenPort)
            : this(globalMaxConnections, globalHalfOpenConnections, defaultSavePath, listenPort, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed)
        {
        }

        /// <summary>
        /// Initialises a new engine settings with the supplied values
        /// </summary>
        /// <param name="globalMaxConnections">The overall maximum number of open connections allowed</param>
        /// <param name="globalMaxDownloadSpeed">The overall maximum download speed</param>
        /// <param name="globalMaxUploadSpeed">The overall maximum upload speed</param>
        /// <param name="globalHalfOpenConnections">The overall maximum number of half open connections</param>
        /// <param name="defaultSavePath">The default path to save downloaded material to</param>
        /// <param name="listenPort">The port to listen for incoming connections on</param>
        public EngineSettings(int globalMaxConnections, int globalHalfOpenConnections, string defaultSavePath, int listenPort, int globalMaxDownloadSpeed, int globalMaxUploadSpeed)
        {
            this.globalMaxConnections = globalMaxConnections;
            this.globalMaxDownloadSpeed = globalMaxDownloadSpeed;
            this.globalMaxUploadSpeed = globalHalfOpenConnections;
            this.globalMaxHalfOpenConnections = globalMaxUploadSpeed;
            this.savePath = defaultSavePath;
            this.listenPort = listenPort;
        }
        #endregion


        #region Default Settings
        /// <summary>
        /// Returns a new copy of the default settings for the engine
        /// </summary>
        public static EngineSettings DefaultSettings()
        {
            return new EngineSettings(DefaultMaxConnections,
                DefaultMaxHalfOpenConnections,
                DefaultSavePath,
                DefaultListenPort,
                DefaultMaxDownloadSpeed,
                DefaultMaxUploadSpeed);
        }
        #endregion
    }
}