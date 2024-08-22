using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace MonoTorrent.Client
{
    public class EngineHelpers
    {
        public static ClientEngine Create ()
            => Create (CreateSettings (), Factories);

        public static ClientEngine Create (EngineSettings settings)
            => Create (settings, Factories);

        public static ClientEngine Create (Factories factories)
            => Create (CreateSettings (), factories);

        public static ClientEngine Create (EngineSettings settings, Factories factories)
            => new ClientEngine (settings, factories);

        public static Factories Factories => Factories.Default
            .WithPieceWriterCreator (t => new TestWriter ());

        internal static EngineSettings CreateSettings (
            bool allowLocalPeerDiscovery = false,
            bool allowPortForwarding = false,
            bool automaticFastResume = false,
            bool autoSaveLoadMagnetLinkMetadata = true,
            IPEndPoint dhtEndPoint = null,
            Dictionary<string, IPEndPoint> listenEndPoints = null,
            string cacheDirectory = null,
            bool usePartialFiles = false)
        {
            return new EngineSettingsBuilder {
                AllowLocalPeerDiscovery = allowLocalPeerDiscovery,
                AllowPortForwarding = allowPortForwarding,
                AutoSaveLoadFastResume = automaticFastResume,
                AutoSaveLoadMagnetLinkMetadata = autoSaveLoadMagnetLinkMetadata,
                CacheDirectory = cacheDirectory ?? Path.Combine (Path.GetDirectoryName (typeof (EngineSettingsBuilder).Assembly.Location)!, "test_cache_dir"),
                DhtEndPoint = dhtEndPoint,
                ListenEndPoints = new Dictionary<string, IPEndPoint> (listenEndPoints ?? new Dictionary<string, IPEndPoint> ()),
                UsePartialFiles = usePartialFiles,
            }.ToSettings ();
        }
    }
}
