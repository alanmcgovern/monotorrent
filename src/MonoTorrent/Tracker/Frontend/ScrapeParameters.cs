using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;

namespace MonoTorrent.Tracker
{
    public class ScrapeParameters : RequestParameters
    {
        public ScrapeParameters(NameValueCollection collection, IPAddress address)
            : base(collection, address)
        {
            InfoHashes = new List<InfoHash>();
            ParseHashes(Parameters["info_hash"]);
        }

        public int Count
        {
            get { return InfoHashes.Count; }
        }

        public List<InfoHash> InfoHashes { get; }

        public override bool IsValid
        {
            get { return true; }
        }

        private void ParseHashes(string infoHash)
        {
            if (string.IsNullOrEmpty(infoHash))
                return;

            if (infoHash.IndexOf(',') > 0)
            {
                var stringHashs = infoHash.Split(',');
                for (var i = 0; i < stringHashs.Length; i++)
                    InfoHashes.Add(InfoHash.UrlDecode(stringHashs[i]));
            }
            else
            {
                InfoHashes.Add(InfoHash.UrlDecode(infoHash));
            }
        }

        public IEnumerator GetEnumerator()
        {
            return InfoHashes.GetEnumerator();
        }
    }
}