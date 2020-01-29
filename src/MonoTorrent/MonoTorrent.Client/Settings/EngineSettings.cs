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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        TimeSpan connectionTimeout = TimeSpan.FromSeconds (10);
        int listenPort = 52138;
        int maximumConnections = 150;
        int maximumDiskReadRate;
        int maximumDiskWriteRate;
        int maximumDownloadSpeed;
        int maximumHalfOpenConnections = 8;
        int maximumOpenFiles = 20;
        int maximumUploadSpeed;

        /// <summary>
        /// A flags enum representing which encryption methods are allowed. Defaults to <see cref="EncryptionTypes.All"/>.
        /// If <see cref="EncryptionTypes.None"/> is set, then encrypted and unencrypted connections will both be disallowed
        /// and no connections will be made. Defaults to <see cref="EncryptionTypes.All"/>.
        /// </summary>
        public EncryptionTypes AllowedEncryption { get; set; } = EncryptionTypes.All;

        /// <summary>
        /// Have suppression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial. Defaults to <see langword="false" />.
        /// </summary>
        public bool AllowHaveSuppression { get; set; } = false;

        /// <summary>
        /// If a connection attempt does not complete within the given timeout, it will be cancelled so
        /// a connection can be attempted with a new peer. Defaults to 10 seconds. It is highly recommended
        /// to keep this value within a range of 7-15 seconds unless absolutely necessary.
        /// </summary>
        public TimeSpan ConnectionTimeout {
            get => connectionTimeout;
            set {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException (nameof (value), "The timeout must be greater than 0");
                connectionTimeout = value;
            }
        }
        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Defaults to 52138.
        /// </summary>
        public int ListenPort {
            get => listenPort;
            set => listenPort = CheckPort (value);
        }

        /// <summary>
        /// The maximum number of concurrent open connections overall. Defaults to 150.
        /// </summary>
        public int MaximumConnections {
            get => maximumConnections;
            set => maximumConnections = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum download speed, in bytes per second, overall. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadSpeed {
            get => maximumDownloadSpeed;
            set => maximumDownloadSpeed = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum number of concurrent connection attempts overall. Defaults to 8.
        /// </summary>
        public int MaximumHalfOpenConnections {
            get => maximumHalfOpenConnections;
            set => maximumHalfOpenConnections = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum upload speed, in bytes per second, overall. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadSpeed {
            get => maximumUploadSpeed;
            set => maximumUploadSpeed = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources. A value of 0 means unlimited, but this is not recommended. Defaults to 20.
        /// </summary>
        public int MaximumOpenFiles {
            get => maximumOpenFiles;
            set => maximumOpenFiles = CheckZeroOrPositive (20);
        }

        /// <summary>
        /// The maximum disk read speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth. Defaults to 0.
        /// </summary>
        public int MaximumDiskReadRate {
            get => maximumDiskReadRate;
            set => maximumDiskReadRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum disk write speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download speed exceeds the max write rate then the
        /// download will be throttled. Defaults to 0.
        /// </summary>
        public int MaximumDiskWriteRate {
            get => maximumDiskWriteRate;
            set => maximumDiskWriteRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// Defaults to <see langword="null" />
        /// </summary>
        public IPEndPoint ReportedAddress { get; set; } = null;

        /// <summary>
        /// If this is set to false and <see cref="AllowedEncryption"/> allows <see cref="EncryptionTypes.PlainText"/>, then
        /// unencrypted connections will be used by default for new outgoing connections. Otherwise, if <see cref="AllowedEncryption"/>
        /// allows <see cref="EncryptionTypes.RC4Full"/> or <see cref="EncryptionTypes.RC4Header"/> then an encrypted connection
        /// will be used by default for new outgoing connections. Defaults to <see langword="true" />.
        /// </summary>
        public bool PreferEncryption { get; set; } = true;

        /// <summary>
        /// This is the path where the .torrent metadata will be saved when magnet links are used to start a download.
        /// Defaults to <see langword="null" />
        /// </summary>
        public string SavePath { get; set; } = null;

        object ICloneable.Clone ()
        {
            return Clone ();
        }

        public EngineSettings Clone ()
        {
            return (EngineSettings) MemberwiseClone ();
        }

        public override bool Equals (object obj)
        {
            return obj is EngineSettings settings
                   && AllowedEncryption == settings.AllowedEncryption
                   && AllowHaveSuppression == settings.AllowHaveSuppression
                   && ListenPort == settings.ListenPort
                   && MaximumConnections == settings.MaximumConnections
                   && MaximumDiskReadRate == settings.MaximumDiskReadRate
                   && MaximumDiskWriteRate == settings.MaximumDiskWriteRate
                   && MaximumDownloadSpeed == settings.MaximumDownloadSpeed
                   && MaximumHalfOpenConnections == settings.MaximumHalfOpenConnections
                   && MaximumOpenFiles == settings.MaximumOpenFiles
                   && MaximumUploadSpeed == settings.MaximumUploadSpeed
                   && PreferEncryption == settings.PreferEncryption
                   && ReportedAddress == settings.ReportedAddress
                   && SavePath == settings.SavePath;
        }

        public override int GetHashCode ()
        {
            return MaximumConnections +
                   MaximumDownloadSpeed +
                   MaximumUploadSpeed +
                   MaximumHalfOpenConnections +
                   ListenPort.GetHashCode () +
                   AllowedEncryption.GetHashCode () +
                   SavePath.GetHashCode ();
        }

        static int CheckPort (int value)
        {
            if (value < 0 || value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be a valid port number between 1 and 65535");
            return value;
        }

        static int CheckZeroOrPositive (int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be zero or greater");
            return value;
        }
    }
}