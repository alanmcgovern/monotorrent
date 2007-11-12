//
// AnnounceParameters.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
using MonoTorrent.Common;
using System.Collections.Specialized;
using System.Collections.Generic;
using MonoTorrent.BEncoding;
using System.Web;
using System.Net;

namespace MonoTorrent.Tracker
{       
    public class AnnounceParameters : RequestParameters
    {
        private static readonly string[] mandatoryFields = { "info_hash", "peer_id", "port", "uploaded", "downloaded", "left", "compact" };

        // FIXME: Expose these as configurable options
        public const int DefaultWanted = 30;
        public const bool UseTrackerKey = false;
        private IPEndPoint clientAddress;
        private bool isValid;

        public IPEndPoint ClientAddress
        {
            get { return clientAddress; }
        }

        public int Downloaded
        {
            get { return ParseInt("downloaded"); }
        }

        public TorrentEvent Event
        {
            get
            {
                string e = Parameters["event"];
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

        public byte[] InfoHash
        {
            get { return HttpUtility.UrlDecodeToBytes(Parameters["info_hash"]); }
        }

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
                int val = ParseInt(Parameters["numwant"]);
                return val != 0 ? val : DefaultWanted;
            }
        }

        public string PeerId
        {
            get { return Parameters["peer_id"]; } 
        }

        public ushort Port
        {
            get { return (ushort)ParseInt("port"); }
        }

        public string TrackerId
        {
            get { return Parameters["trackerid"]; }
        }

        public long Uploaded
        {
            get { return ParseInt("uploaded"); }
        }


        public AnnounceParameters(NameValueCollection collection, IPAddress address)
            : base(collection, address)
        {
            CheckMandatoryFields();
            if (!isValid)
                return;

            /* If the user has supplied an IP address, we use that instead of
             * the IP address we read from the announce request connection. */
            IPAddress supplied;
            if (IPAddress.TryParse(Parameters["ip"] ?? "", out supplied))
                clientAddress = new IPEndPoint(supplied, Port);
            else
                clientAddress = new IPEndPoint(address, Port);
        }


        private void CheckMandatoryFields()
        {
            List<string> keys = new List<string>(Parameters.AllKeys);
            foreach (string field in mandatoryFields)
            {
                if (keys.Contains(field))
                    continue;

                isValid = false;
                Response.Add(FailureKey, (BEncodedString)("mandatory announce parameter " + field + " in query missing"));
                return;
            }
            isValid = true;
        }

        public override int GetHashCode()
        {
            return RemoteAddress.Address.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            AnnounceParameters other = obj as AnnounceParameters;
            return other == null ? false : other.clientAddress.Equals(clientAddress)
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
