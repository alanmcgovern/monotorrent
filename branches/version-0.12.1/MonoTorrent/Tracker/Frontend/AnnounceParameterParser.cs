//
// AnnounceParameterPaser.cs
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
using System.Web;
using System.Collections.Specialized;

using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    
    public class AnnounceParameterParser : ParameterParser
    {        
        string[] mandatoryFields = {"info_hash", "peer_id", "port", "uploaded", "downloaded", "left", "compact"};//make const
        
        public AnnounceParameterParser(string rawUrl) : base(rawUrl)
        {
        
        }
        
        public AnnounceParameters GetParameters()
        {            
            AnnounceParameters par = new AnnounceParameters();
            NameValueCollection fields = GetConvertedQuery();
            CheckMandatoryFields(fields);
            par.infoHash = HttpUtility.UrlDecodeToBytes(fields["info_hash"]);
            
            //fill mandatory parameters
            par.peerId = fields["peer_id"];
            par.port = ushort.Parse(fields["port"]);
            par.uploaded = int.Parse(fields["uploaded"]);
            par.downloaded = int.Parse(fields["downloaded"]);
            par.left = int.Parse(fields["left"]);
            par.compact = fields["compact"].Equals("1");
            
            //fill semi optional parameters                        
            par.@event = TorrentEvent.None;
            string e = fields["event"];    
            if (e != null) {
                if (e.Equals("started")) par.@event = TorrentEvent.Started;
                if (e.Equals("stopped")) par.@event = TorrentEvent.Stopped;
                if (e.Equals("completed")) par.@event = TorrentEvent.Completed;
            }
            
            
            //fill optional parameters
            par.ip = fields["ip"];
            
            par.numberWanted = (fields["numwant"] == null) ? 30 : int.Parse(fields["numwant"]);
            par.key = fields["key"];
            par.trackerId = fields["trackerid"];
                        
            return par;
        }
        
        private void CheckMandatoryFields(NameValueCollection fields)
        {
            foreach (string field in mandatoryFields) {
                if (fields[field] == null) {
                    throw new TrackerException("mandatory announce parameter " + field + " in query missing");
                }
            }
        }
        
        
    }
    
}
