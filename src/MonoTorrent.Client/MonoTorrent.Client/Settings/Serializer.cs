using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Dht;
using MonoTorrent.PieceWriter;

namespace MonoTorrent.Client
{
    static class Serializer
    {
        internal static EngineSettings DeserializeEngineSettings (BEncodedDictionary dict)
        {
            // Start from defaults, then overlay whatever the dictionary contains.
            var defaults = new EngineSettings ();

            return EngineSettings.Create (defaults with {
                AllowedEncryption = dict.TryGetValue (nameof (EngineSettings.AllowedEncryption), out var v0)
                    ? ReadEncryptionList ((BEncodedList) v0)
                    : defaults.AllowedEncryption,

                AllowedTransports = dict.TryGetValue (nameof (EngineSettings.AllowedTransports), out var v31)
                    ? ReadPeerTransportList ((BEncodedList) v31)
                    : defaults.AllowedTransports,

                AllowHaveSuppression = dict.TryGetValue (nameof (EngineSettings.AllowHaveSuppression), out var v1)
                    ? bool.Parse (v1.ToString ()!)
                    : defaults.AllowHaveSuppression,

                AllowLocalPeerDiscovery = dict.TryGetValue (nameof (EngineSettings.AllowLocalPeerDiscovery), out var v2)
                    ? bool.Parse (v2.ToString ()!)
                    : defaults.AllowLocalPeerDiscovery,

                AllowPortForwarding = dict.TryGetValue (nameof (EngineSettings.AllowPortForwarding), out var v3)
                    ? bool.Parse (v3.ToString ()!)
                    : defaults.AllowPortForwarding,

                AutoSaveLoadDhtCache = dict.TryGetValue (nameof (EngineSettings.AutoSaveLoadDhtCache), out var v4)
                    ? bool.Parse (v4.ToString ()!)
                    : defaults.AutoSaveLoadDhtCache,

                AutoSaveLoadFastResume = dict.TryGetValue (nameof (EngineSettings.AutoSaveLoadFastResume), out var v5)
                    ? bool.Parse (v5.ToString ()!)
                    : defaults.AutoSaveLoadFastResume,

                AutoSaveLoadMagnetLinkMetadata = dict.TryGetValue (nameof (EngineSettings.AutoSaveLoadMagnetLinkMetadata), out var v6)
                    ? bool.Parse (v6.ToString ()!)
                    : defaults.AutoSaveLoadMagnetLinkMetadata,

                CacheDirectory = dict.TryGetValue (nameof (EngineSettings.CacheDirectory), out var v7)
                    ? ((BEncodedString) v7).Text
                    : defaults.CacheDirectory,

                ConnectionRetryDelays = dict.TryGetValue (nameof (EngineSettings.ConnectionRetryDelays), out var v8)
                    ? ReadTimeSpanList ((BEncodedList) v8)
                    : defaults.ConnectionRetryDelays,

                ConnectionTimeouts = dict.TryGetValue (nameof (EngineSettings.ConnectionTimeouts), out var v9)
                    ? ReadTimeSpanList ((BEncodedList) v9)
                    : defaults.ConnectionTimeouts,

                DhtBootstrapRouters = dict.TryGetValue (nameof (EngineSettings.DhtBootstrapRouters), out var v10)
                    ? ReadBootstrapRouters ((BEncodedList) v10)
                    : defaults.DhtBootstrapRouters,

                EnableDht = dict.TryGetValue (nameof (EngineSettings.EnableDht), out var v11)
                    ? bool.Parse (v11.ToString ()!)
                    : defaults.EnableDht,

                DiskCacheBytes = dict.TryGetValue (nameof (EngineSettings.DiskCacheBytes), out var v12)
                    ? (int) ((BEncodedNumber) v12).Number
                    : defaults.DiskCacheBytes,

                DiskCachePolicy = dict.TryGetValue (nameof (EngineSettings.DiskCachePolicy), out var v13)
                    ? Enum.Parse<CachePolicy> (((BEncodedString) v13).Text)
                    : defaults.DiskCachePolicy,

                FastResumeMode = dict.TryGetValue (nameof (EngineSettings.FastResumeMode), out var v14)
                    ? Enum.Parse<FastResumeMode> (((BEncodedString) v14).Text)
                    : defaults.FastResumeMode,

                FileCreationOptions = dict.TryGetValue (nameof (EngineSettings.FileCreationOptions), out var v15)
                    ? Enum.Parse<FileCreationOptions> (((BEncodedString) v15).Text)
                    : defaults.FileCreationOptions,

                HttpStreamingPrefix = dict.TryGetValue (nameof (EngineSettings.HttpStreamingPrefix), out var v16)
                    ? ((BEncodedString) v16).Text
                    : defaults.HttpStreamingPrefix,

                ListenEndPoints = dict.TryGetValue (nameof (EngineSettings.ListenEndPoints), out var v17)
                    ? ReadEndPointDictionary ((BEncodedDictionary) v17)
                    : defaults.ListenEndPoints,

                MaximumConnections = dict.TryGetValue (nameof (EngineSettings.MaximumConnections), out var v18)
                    ? (int) ((BEncodedNumber) v18).Number
                    : defaults.MaximumConnections,

                MaximumDiskReadRate = dict.TryGetValue (nameof (EngineSettings.MaximumDiskReadRate), out var v19)
                    ? (int) ((BEncodedNumber) v19).Number
                    : defaults.MaximumDiskReadRate,

                MaximumDiskWriteRate = dict.TryGetValue (nameof (EngineSettings.MaximumDiskWriteRate), out var v20)
                    ? (int) ((BEncodedNumber) v20).Number
                    : defaults.MaximumDiskWriteRate,

                MaximumDownloadRate = dict.TryGetValue (nameof (EngineSettings.MaximumDownloadRate), out var v21)
                    ? (int) ((BEncodedNumber) v21).Number
                    : defaults.MaximumDownloadRate,

                MaximumHalfOpenConnections = dict.TryGetValue (nameof (EngineSettings.MaximumHalfOpenConnections), out var v22)
                    ? (int) ((BEncodedNumber) v22).Number
                    : defaults.MaximumHalfOpenConnections,

                MaximumOpenFiles = dict.TryGetValue (nameof (EngineSettings.MaximumOpenFiles), out var v23)
                    ? (int) ((BEncodedNumber) v23).Number
                    : defaults.MaximumOpenFiles,

                MaximumUploadRate = dict.TryGetValue (nameof (EngineSettings.MaximumUploadRate), out var v24)
                    ? (int) ((BEncodedNumber) v24).Number
                    : defaults.MaximumUploadRate,

                ReportedListenEndPoints = dict.TryGetValue (nameof (EngineSettings.ReportedListenEndPoints), out var v25)
                    ? ReadEndPointDictionary ((BEncodedDictionary) v25)
                    : defaults.ReportedListenEndPoints,

                StaleRequestTimeout = dict.TryGetValue (nameof (EngineSettings.StaleRequestTimeout), out var v26)
                    ? TimeSpan.FromTicks (((BEncodedNumber) v26).Number)
                    : defaults.StaleRequestTimeout,

                UsePartialFiles = dict.TryGetValue (nameof (EngineSettings.UsePartialFiles), out var v27)
                    ? bool.Parse (v27.ToString ()!)
                    : defaults.UsePartialFiles,

                WebSeedConnectionTimeout = dict.TryGetValue (nameof (EngineSettings.WebSeedConnectionTimeout), out var v28)
                    ? TimeSpan.FromTicks (((BEncodedNumber) v28).Number)
                    : defaults.WebSeedConnectionTimeout,

                WebSeedDelay = dict.TryGetValue (nameof (EngineSettings.WebSeedDelay), out var v29)
                    ? TimeSpan.FromTicks (((BEncodedNumber) v29).Number)
                    : defaults.WebSeedDelay,

                WebSeedSpeedTrigger = dict.TryGetValue (nameof (EngineSettings.WebSeedSpeedTrigger), out var v30)
                    ? (int) ((BEncodedNumber) v30).Number
                    : defaults.WebSeedSpeedTrigger,
            });
        }

        internal static TorrentSettings DeserializeTorrentSettings (BEncodedDictionary dict)
        {
            // Start from defaults, then overlay whatever the dictionary contains.
            var defaults = new TorrentSettings ();
            return TorrentSettings.Create (defaults with {
                AllowDht = dict.TryGetValue (nameof (TorrentSettings.AllowDht), out var boolValue)
                    ? bool.Parse (boolValue.ToString ()!)
                    : defaults.AllowDht,

                AllowInitialSeeding = dict.TryGetValue (nameof (TorrentSettings.AllowInitialSeeding), out boolValue)
                    ? bool.Parse (boolValue.ToString ()!)
                    : defaults.AllowInitialSeeding,

                AllowPeerExchange = dict.TryGetValue (nameof (TorrentSettings.AllowPeerExchange), out boolValue)
                    ? bool.Parse (boolValue.ToString ()!)
                    : defaults.AllowPeerExchange,

                CreateContainingDirectory = dict.TryGetValue (nameof (TorrentSettings.CreateContainingDirectory), out boolValue)
                    ? bool.Parse (boolValue.ToString ()!)
                    : defaults.CreateContainingDirectory,

                MaximumConnections = dict.TryGetValue (nameof (TorrentSettings.MaximumConnections), out var longValue)
                    ? (int) ((BEncodedNumber) longValue).Number
                    : defaults.MaximumConnections,

                MaximumDownloadRate = dict.TryGetValue (nameof (TorrentSettings.MaximumDownloadRate), out longValue)
                    ? (int) ((BEncodedNumber) longValue).Number
                    : defaults.MaximumDownloadRate,

                MaximumUploadRate = dict.TryGetValue (nameof (TorrentSettings.MaximumUploadRate), out longValue)
                    ? (int) ((BEncodedNumber) longValue).Number
                    : defaults.MaximumUploadRate,

                RequirePeerIdToMatch = dict.TryGetValue (nameof (TorrentSettings.RequirePeerIdToMatch), out boolValue)
                    ? bool.Parse (boolValue.ToString ()!)
                    : defaults.RequirePeerIdToMatch,

                UploadSlots = dict.TryGetValue (nameof (TorrentSettings.UploadSlots), out longValue)
                    ? (int) ((BEncodedNumber) longValue).Number
                    : defaults.UploadSlots,
            });
        }

        internal static BEncodedDictionary Serialize (TorrentSettings s)
        {
            var dict = new BEncodedDictionary ();

            dict[nameof (s.AllowDht)] = new BEncodedString (s.AllowDht.ToString ());
            dict[nameof (s.AllowInitialSeeding)] = new BEncodedString (s.AllowInitialSeeding.ToString ());
            dict[nameof (s.AllowPeerExchange)] = new BEncodedString (s.AllowPeerExchange.ToString ());
            dict[nameof (s.CreateContainingDirectory)] = new BEncodedString (s.CreateContainingDirectory.ToString ());
            dict[nameof (s.MaximumConnections)] = new BEncodedNumber (s.MaximumConnections);
            dict[nameof (s.MaximumDownloadRate)] = new BEncodedNumber (s.MaximumDownloadRate);
            dict[nameof (s.MaximumUploadRate)] = new BEncodedNumber (s.MaximumUploadRate);
            dict[nameof (s.RequirePeerIdToMatch)] = new BEncodedString (s.RequirePeerIdToMatch.ToString ());
            dict[nameof (s.UploadSlots)] = new BEncodedNumber (s.UploadSlots);

            return dict;
        }

        internal static BEncodedDictionary Serialize (EngineSettings s)
        {
            var dict = new BEncodedDictionary ();

            dict[nameof (s.AllowedEncryption)] = WriteEncryptionList (s.AllowedEncryption);
            dict[nameof (s.AllowedTransports)] = WritePeerTransportList (s.AllowedTransports);
            dict[nameof (s.AllowHaveSuppression)] = new BEncodedString (s.AllowHaveSuppression.ToString ());
            dict[nameof (s.AllowLocalPeerDiscovery)] = new BEncodedString (s.AllowLocalPeerDiscovery.ToString ());
            dict[nameof (s.AllowPortForwarding)] = new BEncodedString (s.AllowPortForwarding.ToString ());
            dict[nameof (s.AutoSaveLoadDhtCache)] = new BEncodedString (s.AutoSaveLoadDhtCache.ToString ());
            dict[nameof (s.AutoSaveLoadFastResume)] = new BEncodedString (s.AutoSaveLoadFastResume.ToString ());
            dict[nameof (s.AutoSaveLoadMagnetLinkMetadata)] = new BEncodedString (s.AutoSaveLoadMagnetLinkMetadata.ToString ());
            dict[nameof (s.CacheDirectory)] = new BEncodedString (s.CacheDirectory);
            dict[nameof (s.ConnectionRetryDelays)] = WriteTimeSpanList (s.ConnectionRetryDelays);
            dict[nameof (s.ConnectionTimeouts)] = WriteTimeSpanList (s.ConnectionTimeouts);
            dict[nameof (s.DhtBootstrapRouters)] = WriteBootstrapRouters (s.DhtBootstrapRouters);
            dict[nameof (s.EnableDht)] = new BEncodedString (s.EnableDht.ToString ());
            dict[nameof (s.DiskCacheBytes)] = new BEncodedNumber (s.DiskCacheBytes);
            dict[nameof (s.DiskCachePolicy)] = new BEncodedString (s.DiskCachePolicy.ToString ());
            dict[nameof (s.FastResumeMode)] = new BEncodedString (s.FastResumeMode.ToString ());
            dict[nameof (s.FileCreationOptions)] = new BEncodedString (s.FileCreationOptions.ToString ());
            dict[nameof (s.HttpStreamingPrefix)] = new BEncodedString (s.HttpStreamingPrefix);
            dict[nameof (s.ListenEndPoints)] = WriteEndPointDictionary (s.ListenEndPoints);
            dict[nameof (s.MaximumConnections)] = new BEncodedNumber (s.MaximumConnections);
            dict[nameof (s.MaximumDiskReadRate)] = new BEncodedNumber (s.MaximumDiskReadRate);
            dict[nameof (s.MaximumDiskWriteRate)] = new BEncodedNumber (s.MaximumDiskWriteRate);
            dict[nameof (s.MaximumDownloadRate)] = new BEncodedNumber (s.MaximumDownloadRate);
            dict[nameof (s.MaximumHalfOpenConnections)] = new BEncodedNumber (s.MaximumHalfOpenConnections);
            dict[nameof (s.MaximumOpenFiles)] = new BEncodedNumber (s.MaximumOpenFiles);
            dict[nameof (s.MaximumUploadRate)] = new BEncodedNumber (s.MaximumUploadRate);
            dict[nameof (s.ReportedListenEndPoints)] = WriteEndPointDictionary (s.ReportedListenEndPoints);
            dict[nameof (s.StaleRequestTimeout)] = new BEncodedNumber (s.StaleRequestTimeout.Ticks);
            dict[nameof (s.UsePartialFiles)] = new BEncodedString (s.UsePartialFiles.ToString ());
            dict[nameof (s.WebSeedConnectionTimeout)] = new BEncodedNumber (s.WebSeedConnectionTimeout.Ticks);
            dict[nameof (s.WebSeedDelay)] = new BEncodedNumber (s.WebSeedDelay.Ticks);
            dict[nameof (s.WebSeedSpeedTrigger)] = new BEncodedNumber (s.WebSeedSpeedTrigger);

            return dict;
        }

        static ImmutableArray<EncryptionType> ReadEncryptionList (BEncodedList list)
        {
            var result = new List<EncryptionType> (list.Count);
            foreach (BEncodedString item in list)
                result.Add (Enum.Parse<EncryptionType> (item.Text));
            return result.ToImmutableArray ();
        }

        static ImmutableArray<PeerTransport> ReadPeerTransportList (BEncodedList list)
        {
            var result = new List<PeerTransport> (list.Count);
            foreach (BEncodedString item in list)
                result.Add (Enum.Parse<PeerTransport> (item.Text));
            return result.ToImmutableArray ();
        }

        static ImmutableArray<BootstrapRouter> ReadBootstrapRouters (BEncodedList list)
        {
            var result = new List<BootstrapRouter> (list.Count);
            foreach (BEncodedList router in list)
                result.Add (new BootstrapRouter (((BEncodedString) router[0]).Text, (int) ((BEncodedNumber) router[1]).Number));
            return result.ToImmutableArray ();
        }

        static ImmutableDictionary<string, IPEndPoint> ReadEndPointDictionary (BEncodedDictionary dict)
        {
            var result = new Dictionary<string, IPEndPoint> (dict.Count);
            foreach (var kvp in dict) {
                var parts = (BEncodedList) kvp.Value;
                result[kvp.Key.Text] = new IPEndPoint (
                    IPAddress.Parse (((BEncodedString) parts[0]).Text),
                    (int) ((BEncodedNumber) parts[1]).Number);
            }
            return result.ToImmutableDictionary ();
        }

        static ImmutableArray<TimeSpan> ReadTimeSpanList (BEncodedList list)
        {
            var result = new List<TimeSpan> (list.Count);
            foreach (BEncodedNumber n in list)
                result.Add (TimeSpan.FromTicks (n.Number));
            return result.ToImmutableArray ();
        }

        static BEncodedList WriteEncryptionList (IList<EncryptionType> value)
            => new BEncodedList (value.Select (v => (BEncodedValue) new BEncodedString (v.ToString ())));

        static BEncodedList WritePeerTransportList (IList<PeerTransport> value)
            => new BEncodedList (value.Select (v => (BEncodedValue) new BEncodedString (v.ToString ())));

        static BEncodedList WriteBootstrapRouters (IList<BootstrapRouter> value)
        {
            var list = new BEncodedList (value.Count);
            foreach (var r in value)
                list.Add (new BEncodedList { (BEncodedString) r.Host, (BEncodedNumber) r.Port });
            return list;
        }

        static BEncodedList WriteNullableEndPoint (IPEndPoint? ep)
            => ep is null
                ? new BEncodedList ()
                : new BEncodedList { (BEncodedString) ep.Address.ToString (), (BEncodedNumber) ep.Port };

        static BEncodedDictionary WriteEndPointDictionary (IDictionary<string, IPEndPoint> value)
        {
            var dict = new BEncodedDictionary ();
            foreach (var kvp in value)
                dict[(BEncodedString) kvp.Key] = new BEncodedList {
                    (BEncodedString) kvp.Value.Address.ToString (),
                    (BEncodedNumber) kvp.Value.Port
                };
            return dict;
        }

        static BEncodedList WriteTimeSpanList (IList<TimeSpan> value)
            => new BEncodedList (value.Select (v => (BEncodedValue) new BEncodedNumber (v.Ticks)));
    }
}
