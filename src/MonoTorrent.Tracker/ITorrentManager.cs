//
// ITorrentManager.cs
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

namespace MonoTorrent.Tracker
{
    ///<summary>this class is responsible for managing all the information surrounding
    ///this particular torrent. this includes the peers downloading/uploading this particular torrent
    ///</summary>
    public interface ITorrentManager
    {
        ///<summary>identifies the torrent which we are managing</summary>
        Torrent Torrent
        {
            get;
        }
        
        ///<summary>counts all seeders.</summary>
        int CountComplete
        {
            get;
        }
        
        ///<summary>return how often the torrent was fully downloaded.</summary>
        int Downloaded
        {
            get;
        }
        
        ///<summary>counts the peers downloading/uploading this torrent.</summary>
        int Count 
        {
            get;
        }
        
        ///<summary>
        ///this method is used for computing the list of peers which share this torrent
        ///</summary>
        ///<returns>if par.compact is true then the value is a BEncodedString otherwise it's a BEncodedDictionary
        ///</returns>
        IBEncodedValue GetPeersList(AnnounceParameters par);     
        
        
        ///<summary>this method returns the scrape entry for this torrent</summary>
        BEncodedDictionary GetScrapeEntry();
        
        ///<summary>adds a new peer with par.ip and par.port</summary>      
        void Add(AnnounceParameters par);
        
        ///<summary>removes the peer with par.ip and par.port</summary>
        void Remove(AnnounceParameters par);
        
        ///<summary>update the internale torrent datas and the peer with par.ip and par.port</summary>
        void Update(AnnounceParameters par);
        
    }   
}
