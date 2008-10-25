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



using MonoTorrent.Client.Encryption;
using System.Reflection;
using System;
using System.Net;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        #region Private Fields

        private bool haveSupressionEnabled;             // True if you want to enable have surpression
        private EncryptionTypes allowedEncryption;      // The minimum encryption level to use. "None" corresponds to no encryption.
        private int listenPort;                         // The port to listen to incoming connections on
        private int globalMaxConnections;               // The maximum number of connections that can be opened
        private int globalMaxHalfOpenConnections;       // The maximum number of simultaenous 1/2 open connections
        private int globalMaxDownloadSpeed;             // The maximum combined download speed
        private int globalMaxUploadSpeed;               // The maximum combined upload speed
        private int maxOpenStreams = 15;                // The maximum number of simultaenous open filestreams
        private int maxReadRate;                        // The maximum read rate from the harddisk (for all active torrentmanagers)
        private int maxWriteRate;                       // The maximum write rate to the harddisk (for all active torrentmanagers)
        private bool preferEncryption;                  // If encrypted and unencrypted connections are enabled, specifies if encryption should be chosen first
        private IPEndPoint reportedEndpoint;            // The IPEndpoint reported to the tracker
        private string savePath;                        // The path that torrents will be downloaded to by default

        #endregion Private Fields


        #region Properties

        public EncryptionTypes AllowedEncryption
        {
            get { return this.allowedEncryption; }
            set { this.allowedEncryption = value; }
        }
		
        public bool HaveSupressionEnabled
        {
            get { return this.haveSupressionEnabled; }
            set { this.haveSupressionEnabled = value; }
        }

        public int GlobalMaxConnections
        {
            get { return this.globalMaxConnections; }
            set { this.globalMaxConnections = value; }
        }

        public int GlobalMaxHalfOpenConnections
        {
            get { return this.globalMaxHalfOpenConnections; }
            set { this.globalMaxHalfOpenConnections = value; }
        }

        public int GlobalMaxDownloadSpeed
        {
            get { return this.globalMaxDownloadSpeed; }
            set { this.globalMaxDownloadSpeed = value; }
        }

        public int GlobalMaxUploadSpeed
        {
            get { return this.globalMaxUploadSpeed; }
            set { this.globalMaxUploadSpeed = value; }
        }
        
        [Obsolete("Use the constructor overload for ClientEngine which takes a port argument." +
                  "Alternatively just use the ChangeEndpoint method at a later stage")]
        public int ListenPort
        {
            get { return this.listenPort; }
            set { this.listenPort = value; }
        }

        public int MaxOpenFiles
        {
            get { return maxOpenStreams; }
            set { maxOpenStreams = value; }
        }

        public int MaxReadRate
        {
            get { return maxReadRate; }
            set { maxReadRate = value; }
        }

        public int MaxWriteRate
        {
            get { return maxWriteRate; }
            set { maxWriteRate = value; }
        }

        public IPEndPoint ReportedAddress
        {
            get { return reportedEndpoint; }
            set { reportedEndpoint = value; }
        }

        public bool PreferEncryption
        {
            get { return preferEncryption; }
            set { preferEncryption = value; }
        }
		
        public string SavePath
        {
            get { return this.savePath; }
            set { this.savePath = value; }
        }

        #endregion Properties


        #region Defaults

        private const bool DefaultEnableHaveSupression = false;
        private const string DefaultSavePath = "";
        private const int DefaultMaxConnections = 150;
        private const int DefaultMaxDownloadSpeed = 0;
        private const int DefaultMaxUploadSpeed = 0;
        private const int DefaultMaxHalfOpenConnections = 5;
        private const EncryptionTypes DefaultAllowedEncryption = EncryptionTypes.All;
        private const int DefaultListenPort = 52138;

        #endregion


        #region Constructors

        public EngineSettings()
            : this(DefaultSavePath, DefaultListenPort, DefaultMaxConnections, DefaultMaxHalfOpenConnections,
                  DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {

        }

        public EngineSettings(string defaultSavePath, int listenPort)
            : this(defaultSavePath, listenPort, DefaultMaxConnections, DefaultMaxHalfOpenConnections, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {

        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections)
            : this(defaultSavePath, listenPort, globalMaxConnections, DefaultMaxHalfOpenConnections, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {

        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections, int globalHalfOpenConnections)
            : this(defaultSavePath, listenPort, globalMaxConnections, globalHalfOpenConnections, DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {

        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections, int globalHalfOpenConnections, int globalMaxDownloadSpeed, int globalMaxUploadSpeed, EncryptionTypes allowedEncryption)
        {
            this.globalMaxConnections = globalMaxConnections;
            this.globalMaxDownloadSpeed = globalMaxDownloadSpeed;
            this.globalMaxUploadSpeed = globalMaxUploadSpeed;
            this.globalMaxHalfOpenConnections = globalHalfOpenConnections;
            this.listenPort = listenPort;
            this.allowedEncryption = allowedEncryption;
            this.savePath = defaultSavePath;
        }
 
        #endregion


        #region Methods

        object ICloneable.Clone()
        {
            return Clone();
        }

        public EngineSettings Clone()
        {
            return (EngineSettings)MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            EngineSettings settings = obj as EngineSettings;
            return (settings == null) ? false : this.globalMaxConnections == settings.globalMaxConnections &&
                                                this.globalMaxDownloadSpeed == settings.globalMaxDownloadSpeed &&
                                                this.globalMaxHalfOpenConnections == settings.globalMaxHalfOpenConnections &&
                                                this.globalMaxUploadSpeed == settings.globalMaxUploadSpeed &&
                                                this.listenPort == settings.listenPort &&
                                                this.allowedEncryption == settings.allowedEncryption &&
                                                this.savePath == settings.savePath;
        }

        public override int GetHashCode()
        {
            return this.globalMaxConnections +
                   this.globalMaxDownloadSpeed +
                   this.globalMaxHalfOpenConnections +
                   this.globalMaxUploadSpeed +
                   this.listenPort.GetHashCode() +
                   this.allowedEncryption.GetHashCode() +
                   this.savePath.GetHashCode();
            
        }

        #endregion Methods
    }
}