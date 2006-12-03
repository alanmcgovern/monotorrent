//
// Peer.cs
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
using System.Collections.Generic;
using System.Text;
using System.Net;
using MonoTorrent.Common;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    public class Peer
    {
        #region Member Variables
        /// <summary>
        /// The connection associated with this peer
        /// </summary>
        internal PeerConnectionBase Connection
        {
            get { return this.connection; }
            set { this.connection = value; }
        }
        private PeerConnectionBase connection;


        /// <summary>
        /// Returns the number of times the peer has sent us a piece which failed a hashcheck
        /// </summary>
        public int HashFails
        {
            get { return this.hashFails; }
            internal set { this.hashFails = value; }
        }
        private int hashFails;


        /// <summary>
        /// The ID of the peer
        /// </summary>
        public string PeerId
        {
            get { return peerId; }
            internal set { peerId = value; }
        }
        private string peerId;


        /// <summary>
        /// True if the peer is a seeder
        /// </summary>
        public bool IsSeeder
        {
            get { return this.isSeeder; }
            internal set { this.isSeeder = value; }
        }
        private bool isSeeder;


        /// <summary>
        /// The number of times we tried to connect to the peer and failed
        /// </summary>
        public int FailedConnectionAttempts
        {
            get { return this.failedConnectionAttempts; }
            internal set { this.failedConnectionAttempts = value; }
        }
        private int failedConnectionAttempts;


        /// <summary>
        /// The location at which the peer can be connected to at
        /// </summary>
        public string Location
        {
            get { return this.location; }
        }
        private string location;
        #endregion


        #region Constructors
        public Peer(string peerId, string location)
        {
            this.peerId = peerId;
            this.location = location;
        }
        #endregion


        #region Overidden Methods
        public override bool Equals(object obj)
        {
            Peer peer = obj as Peer;
            if(peer ==null)
                return false;

            return this.location.Equals(peer.location);
        }


        public override int GetHashCode()
        {
            return this.location.GetHashCode();
        }


        public override string ToString()
        {
            return this.location;
        }
        #endregion

        public StringBuilder MessageHistory = new StringBuilder(1024);
    }
}