using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using MonoTorrent.BEncoding;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    public class AnnounceParameters : RequestParameters
    {
        private static readonly string[] mandatoryFields =
        {
            "info_hash", "peer_id", "port", "uploaded", "downloaded", "left", "compact"
        };

        // FIXME: Expose these as configurable options
        internal static readonly int DefaultWanted = 30;
        internal static readonly bool UseTrackerKey = false;
        private bool isValid;


        public AnnounceParameters(NameValueCollection collection, IPAddress address)
            : base(collection, address)
        {
            CheckMandatoryFields();
            if (!isValid)
                return;

            /* If the user has supplied an IP address, we use that instead of
             * the IP address we read from the announce request connection. */
            IPAddress supplied;
            if (IPAddress.TryParse(Parameters["ip"] ?? "", out supplied) && !supplied.Equals(IPAddress.Any))
                ClientAddress = new IPEndPoint(supplied, Port);
            else
                ClientAddress = new IPEndPoint(address, Port);
        }

        public IPEndPoint ClientAddress { get; }

        public int Downloaded
        {
            get { return ParseInt("downloaded"); }
        }

        public TorrentEvent Event
        {
            get
            {
                var e = Parameters["event"];
                if (e != null)
                {
                    if (e.Equals("started"))
                        return TorrentEvent.Started;
                    if (e.Equals("stopped"))
                        return TorrentEvent.Stopped;
                    if (e.Equals("completed"))
                        return TorrentEvent.Completed;
                }

                return TorrentEvent.None;
            }
        }

        public int Left
        {
            get { return ParseInt("left"); }
        }

        public bool HasRequestedCompact
        {
            get { return ParseInt("compact") == 1; }
        }

        public InfoHash InfoHash { get; private set; }

        public string Key
        {
            get { return Parameters["key"]; }
        }

        public override bool IsValid
        {
            get { return isValid; }
        }

        public int NumberWanted
        {
            get
            {
                var val = ParseInt(Parameters["numwant"]);
                return val != 0 ? val : DefaultWanted;
            }
        }

        public string PeerId
        {
            get { return Parameters["peer_id"]; }
        }

        public int Port
        {
            get { return ParseInt("port"); }
        }

        public string TrackerId
        {
            get { return Parameters["trackerid"]; }
        }

        public long Uploaded
        {
            get { return ParseInt("uploaded"); }
        }


        private void CheckMandatoryFields()
        {
            isValid = false;

            var keys = new List<string>(Parameters.AllKeys);
            foreach (var field in mandatoryFields)
            {
                if (keys.Contains(field))
                    continue;

                Response.Add(FailureKey,
                    (BEncodedString) ("mandatory announce parameter " + field + " in query missing"));
                return;
            }
            var hash = UriHelper.UrlDecode(Parameters["info_hash"]);
            if (hash.Length != 20)
            {
                Response.Add(FailureKey,
                    (BEncodedString)
                        string.Format("infohash was {0} bytes long, it must be 20 bytes long.", hash.Length));
                return;
            }
            InfoHash = new InfoHash(hash);
            isValid = true;
        }

        public override int GetHashCode()
        {
            return RemoteAddress.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as AnnounceParameters;
            return other == null
                ? false
                : other.ClientAddress.Equals(ClientAddress)
                  && other.Port.Equals(Port);
        }

        private int ParseInt(string str)
        {
            int p;
            str = Parameters[str];
            if (!int.TryParse(str, out p))
                p = 0;
            return p;
        }
    }
}