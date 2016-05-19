using System;
using System.Net;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    /// <summary>
    ///     Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        #region Private Fields

        private bool haveSupressionEnabled; // True if you want to enable have surpression

        private EncryptionTypes allowedEncryption;
        // The minimum encryption level to use. "None" corresponds to no encryption.

        private int listenPort; // The port to listen to incoming connections on
        private int globalMaxConnections; // The maximum number of connections that can be opened
        private int globalMaxHalfOpenConnections; // The maximum number of simultaenous 1/2 open connections
        private int globalMaxDownloadSpeed; // The maximum combined download speed
        private int globalMaxUploadSpeed; // The maximum combined upload speed
        private int maxOpenStreams = 15; // The maximum number of simultaenous open filestreams
        private int maxReadRate; // The maximum read rate from the harddisk (for all active torrentmanagers)
        private int maxWriteRate; // The maximum write rate to the harddisk (for all active torrentmanagers)

        private bool preferEncryption;
        // If encrypted and unencrypted connections are enabled, specifies if encryption should be chosen first

        private IPEndPoint reportedEndpoint; // The IPEndpoint reported to the tracker
        private string savePath; // The path that torrents will be downloaded to by default

        #endregion Private Fields

        #region Properties

        public EncryptionTypes AllowedEncryption
        {
            get { return allowedEncryption; }
            set { allowedEncryption = value; }
        }

        public bool HaveSupressionEnabled
        {
            get { return haveSupressionEnabled; }
            set { haveSupressionEnabled = value; }
        }

        public int GlobalMaxConnections
        {
            get { return globalMaxConnections; }
            set { globalMaxConnections = value; }
        }

        public int GlobalMaxHalfOpenConnections
        {
            get { return globalMaxHalfOpenConnections; }
            set { globalMaxHalfOpenConnections = value; }
        }

        public int GlobalMaxDownloadSpeed
        {
            get { return globalMaxDownloadSpeed; }
            set { globalMaxDownloadSpeed = value; }
        }

        public int GlobalMaxUploadSpeed
        {
            get { return globalMaxUploadSpeed; }
            set { globalMaxUploadSpeed = value; }
        }

        [Obsolete("Use the constructor overload for ClientEngine which takes a port argument." +
                  "Alternatively just use the ChangeEndpoint method at a later stage")]
        public int ListenPort
        {
            get { return listenPort; }
            set { listenPort = value; }
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
            get { return savePath; }
            set { savePath = value; }
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
            : this(
                defaultSavePath, listenPort, DefaultMaxConnections, DefaultMaxHalfOpenConnections,
                DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {
        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections)
            : this(
                defaultSavePath, listenPort, globalMaxConnections, DefaultMaxHalfOpenConnections,
                DefaultMaxDownloadSpeed, DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {
        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections,
            int globalHalfOpenConnections)
            : this(
                defaultSavePath, listenPort, globalMaxConnections, globalHalfOpenConnections, DefaultMaxDownloadSpeed,
                DefaultMaxUploadSpeed, DefaultAllowedEncryption)
        {
        }

        public EngineSettings(string defaultSavePath, int listenPort, int globalMaxConnections,
            int globalHalfOpenConnections, int globalMaxDownloadSpeed, int globalMaxUploadSpeed,
            EncryptionTypes allowedEncryption)
        {
            this.globalMaxConnections = globalMaxConnections;
            this.globalMaxDownloadSpeed = globalMaxDownloadSpeed;
            this.globalMaxUploadSpeed = globalMaxUploadSpeed;
            globalMaxHalfOpenConnections = globalHalfOpenConnections;
            this.listenPort = listenPort;
            this.allowedEncryption = allowedEncryption;
            savePath = defaultSavePath;
        }

        #endregion

        #region Methods

        object ICloneable.Clone()
        {
            return Clone();
        }

        public EngineSettings Clone()
        {
            return (EngineSettings) MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            var settings = obj as EngineSettings;
            return settings == null
                ? false
                : globalMaxConnections == settings.globalMaxConnections &&
                  globalMaxDownloadSpeed == settings.globalMaxDownloadSpeed &&
                  globalMaxHalfOpenConnections == settings.globalMaxHalfOpenConnections &&
                  globalMaxUploadSpeed == settings.globalMaxUploadSpeed &&
                  listenPort == settings.listenPort &&
                  allowedEncryption == settings.allowedEncryption &&
                  savePath == settings.savePath;
        }

        public override int GetHashCode()
        {
            return globalMaxConnections +
                   globalMaxDownloadSpeed +
                   globalMaxHalfOpenConnections +
                   globalMaxUploadSpeed +
                   listenPort.GetHashCode() +
                   allowedEncryption.GetHashCode() +
                   savePath.GetHashCode();
        }

        #endregion Methods
    }
}