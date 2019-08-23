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


using System;
using System.Net;

using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        /// <summary>
        /// A flags enum representing which encryption methods are allowed. Defaults to <see cref="EncryptionTypes.All"/>.
        /// If <see cref="EncryptionTypes.None"/> is set, then encrypted and unencrypted connections will both be disallowed
        /// and no connections will be made.
        /// </summary>
        public EncryptionTypes AllowedEncryption { get; set; }

        /// <summary>
        /// Have surpression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial.
        /// </summary>
        public bool HaveSupressionEnabled { get; set; }

        /// <summary>
        /// The maximum number of concurrent open connections overall.
        /// </summary>
        public int GlobalMaxConnections { get; set; }

        /// <summary>
        /// The maximum number of concurrent connection attempts overall.
        /// </summary>
        public int GlobalMaxHalfOpenConnections { get; set; }

        /// <summary>
        /// The maximum download speed, in bytes per second, overall. A value of 0 means unlimited.
        /// </summary>
        public int GlobalMaxDownloadSpeed { get; set; }

        /// <summary>
        /// The maximum upload speed, in bytes per second, overall.  A value of 0 means unlimited.
        /// </summary>
        public int GlobalMaxUploadSpeed { get; set; }
        
        //[Obsolete("Use the constructor overload for ClientEngine which takes a port argument." +
        //          "Alternatively just use the ChangeEndpoint method at a later stage")]
        public int ListenPort { get; set; }

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources.
        /// </summary>
        public int MaxOpenFiles { get; set; }

        /// <summary>
        /// The maximum disk read speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth.
        /// </summary>
        public int MaxDiskReadRate { get; set; }

        /// <summary>
        /// The maximum disk write speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download speed exceeds the max write rate then the
        /// download will be throttled.
        /// </summary>
        public int MaxDiskWriteRate { get; set; }

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// </summary>
        public IPEndPoint ReportedAddress { get; set; }

        /// <summary>
        /// If this is set to false and <see cref="AllowedEncryption"/> allows <see cref="EncryptionTypes.PlainText"/>, then
        /// unencrypted connections will be used by default for new outgoing connections. Otherwise, if <see cref="AllowedEncryption"/>
        /// allows <see cref="EncryptionTypes.RC4Full"/> or <see cref="EncryptionTypes.RC4Header"/> then an encrypted connection
        /// will be used by default for new outgoing connections.
        /// </summary>
        public bool PreferEncryption { get; set; }

        /// <summary>
        /// This is the path where the .torrent metadata will be saved when magnet links are used to start a download.
        /// </summary>
        public string SavePath { get; set; }


        #region Defaults

        private const bool DefaultEnableHaveSupression = false;
        private const string DefaultSavePath = "";
        private const int DefaultMaxConnections = 150;
        private const int DefaultMaxDownloadSpeed = 0;
        private const int DefaultMaxOpenStreams = 20;
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
            AllowedEncryption = allowedEncryption;
            HaveSupressionEnabled = DefaultEnableHaveSupression;
            GlobalMaxConnections = globalMaxConnections;
            GlobalMaxDownloadSpeed = globalMaxDownloadSpeed;
            GlobalMaxUploadSpeed = globalMaxUploadSpeed;
            GlobalMaxHalfOpenConnections = globalHalfOpenConnections;
            ListenPort = listenPort;
            MaxOpenFiles = DefaultMaxOpenStreams;
            SavePath = defaultSavePath;
        }
 
        #endregion


        object ICloneable.Clone()
            => Clone();

        public EngineSettings Clone()
            => (EngineSettings)MemberwiseClone();

        public override bool Equals(object obj)
        {
            EngineSettings settings = obj as EngineSettings;
            return (settings == null) ? false : GlobalMaxConnections == settings.GlobalMaxConnections &&
                                                GlobalMaxDownloadSpeed == settings.GlobalMaxDownloadSpeed &&
                                                GlobalMaxHalfOpenConnections == settings.GlobalMaxHalfOpenConnections &&
                                                GlobalMaxUploadSpeed == settings.GlobalMaxUploadSpeed &&
                                                ListenPort == settings.ListenPort &&
                                                AllowedEncryption == settings.AllowedEncryption &&
                                                SavePath == settings.SavePath;
        }

        public override int GetHashCode()
        {
            return GlobalMaxConnections +
                   GlobalMaxDownloadSpeed +
                   GlobalMaxHalfOpenConnections +
                   GlobalMaxUploadSpeed +
                   ListenPort.GetHashCode() +
                   AllowedEncryption.GetHashCode() +
                   SavePath.GetHashCode();
        }
    }
}