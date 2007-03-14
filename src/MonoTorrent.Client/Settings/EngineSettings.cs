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

        #region Private Fields

        private bool allowLegacyConnections;            // True if you want to allowing non-encrypted incoming connections. Returns true if encrytion is off
        private EncryptionType minEncryptionLevel;      // The minimum encryption level to use. "None" corresponds to no encryption.
        private int listenPort;                         // The port to listen to incoming connections on
        private int globalMaxConnections;               // The maximum number of connections that can be opened
        private int globalMaxHalfOpenConnections;       // The maximum number of simultaenous 1/2 open connections
        private int globalMaxDownloadSpeed;             // The maximum combined download speed
        private int globalMaxUploadSpeed;               // The maximum combined upload speed
        private string savePath;                        // The path that torrents will be downloaded to by default
        private bool useuPnP;                           // True if uPnP should be used to map ports when available

        #endregion Private Fields


        #region Properties

        /// <summary>
        /// This specifies whether non-encrypted incoming connections should be accepted or denied. This setting returns
        /// true if MinEncryption level is set to "None"
        /// </summary>
        public bool AllowLegacyConnections
        {
            get { return (this.allowLegacyConnections) || (this.minEncryptionLevel == EncryptionType.None); }
            set { this.allowLegacyConnections = value; }
        }


        /// <summary>
        /// Specifies the minimum encryption level to use for outgoing connections.
        /// </summary>
        public EncryptionType MinEncryptionLevel
        {
            get { return this.minEncryptionLevel; }
            set { this.minEncryptionLevel = value; }
        }


        /// <summary>
        /// This is the default directory that torrents will be downloaded to
        /// </summary>
        public string SavePath
        {
            get { return this.savePath; }
            set { this.savePath = value; }
        }


        /// <summary>
        /// This is the combined maximum open connections for all running torrents
        /// </summary>
        public int GlobalMaxConnections
        {
            get { return this.globalMaxConnections; }
            set { this.globalMaxConnections = value; }
        }


        /// <summary>
        /// This is the maximum half-open connections allowed
        /// </summary>
        public int GlobalMaxHalfOpenConnections
        {
            get { return this.globalMaxHalfOpenConnections; }
            set { this.globalMaxHalfOpenConnections = value; }
        }


        /// <summary>
        /// This is the combined maximum download speed for all running torrents
        /// </summary>
        public int GlobalMaxDownloadSpeed
        {
            get { return this.globalMaxDownloadSpeed; }
            set { this.globalMaxDownloadSpeed = value; }
        }


        /// <summary>
        /// This is the combined maximum upload speed for all running torrents
        /// </summary>
        public int GlobalMaxUploadSpeed
        {
            get { return this.globalMaxUploadSpeed; }
            set { this.globalMaxUploadSpeed = value; }
        }


        /// <summary>
        /// The port to listen for incoming connections on
        /// </summary>
        public int ListenPort
        {
            get { return this.listenPort; }
            set { this.listenPort = value; }
        }


        /// <summary>
        /// True if the engine should use uPnP to map ports if it is available
        /// </summary>
        public bool UsePnP
        {
            get { return this.useuPnP; }
        }

        #endregion Properties


        #region Defaults

        private const string DefaultSavePath = "";
        private const int DefaultMaxConnections = 150;
        private const int DefaultMaxDownloadSpeed = 0;
        private const int DefaultMaxUploadSpeed = 0;
        private const int DefaultMaxHalfOpenConnections = 5;
        private const int DefaultListenPort = 52138;
        private const bool DefaultUseuPnP = false;              // UPnP crashes on vista's .NET framework, so we won't enable by default

        #endregion


        #region Constructors

        private EngineSettings()
        {
        }

        public EngineSettings(string defaultSavePath, bool useuPnP)
            : this(defaultSavePath, useuPnP, DefaultMaxConnections, DefaultMaxHalfOpenConnections, DefaultListenPort, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed)
        {

        }

        /// <summary>
        /// Initialises a new engine settings with the supplied values
        /// </summary>
        /// <param name="globalMaxConnections">The overall maximum number of open connections allowed</param>
        /// <param name="globalHalfOpenConnections">The overall maximum number of half open connections</param>
        /// <param name="defaultSavePath">The default path to save downloaded material to</param>
        /// <param name="listenPort">The port to listen for incoming connections on</param>
        public EngineSettings(string defaultSavePath, bool useuPnP, int globalMaxConnections, int globalHalfOpenConnections)
            : this(defaultSavePath, useuPnP, globalMaxConnections, globalHalfOpenConnections, DefaultListenPort, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed)
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
        public EngineSettings(string defaultSavePath, bool useuPnP, int globalMaxConnections, int globalHalfOpenConnections, int listenPort, int globalMaxDownloadSpeed, int globalMaxUploadSpeed)
        {
            this.globalMaxConnections = globalMaxConnections;
            this.globalMaxDownloadSpeed = globalMaxDownloadSpeed;
            this.globalMaxUploadSpeed = globalMaxUploadSpeed;
            this.globalMaxHalfOpenConnections = globalHalfOpenConnections;
            this.savePath = defaultSavePath;
            this.listenPort = listenPort;
            this.useuPnP = useuPnP;
        }
        
        #endregion


        #region Default Settings
        /// <summary>
        /// Returns a new copy of the default settings for the engine
        /// </summary>
        public static EngineSettings DefaultSettings()
        {
            return new EngineSettings(DefaultSavePath,
                                     DefaultUseuPnP,
                                     DefaultMaxConnections,
                                     DefaultMaxHalfOpenConnections,
                                     DefaultListenPort,
                                     DefaultMaxDownloadSpeed,
                                     DefaultMaxUploadSpeed);
        }
        #endregion
    }
}