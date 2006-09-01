//
// Peers.cs
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



using System.Collections.Generic;

using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents a list of Peers that can be downloaded from
    /// </summary>
    public class Peers
    {
        #region Member Variables
        private List<PeerConnectionID> peerList;
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new Peer list
        /// </summary>
        public Peers()
            : this(16)
        {
        }


        /// <summary>
        /// Creates a new Peer list
        /// </summary>
        /// <param name="capacity">The initial capacity of the list</param>
        public Peers(int capacity)
        {
            this.peerList = new List<PeerConnectionID>(capacity);
        }
        #endregion


        #region Methods
        /// <summary>
        /// Returns the PeerConnectionID at the specified index
        /// </summary>
        /// <param name="index">The index of the PeerConnectionID to return</param>
        /// <returns></returns>
        public PeerConnectionID this[int index]
        {
            get { return this.peerList[index]; }
            set { this.peerList[index] = value; }
        }


        /// <summary>
        /// Adds a peer to the PeerConnectionID
        /// </summary>
        /// <param name="peer">The peer to add</param>
        public void Add(PeerConnectionID peer)
        {
            this.peerList.Add(peer);
        }


        /// <summary>
        /// Removes the PeerConnectionID at the specified index
        /// </summary>
        /// <param name="index">The index to remove the PeerConnectionID at</param>
        public void Remove(int index)
        {
            this.peerList.RemoveAt(index);
        }


        /// <summary>
        /// Removes the supplied PeerConnectionID from the list
        /// </summary>
        /// <param name="id">The PeerConnectionID to remove</param>
        /// <returns>True if the PeerConnectionID was removed</returns>
        public bool Remove(PeerConnectionID id)
        {
            return this.peerList.Remove(id);
        }


        /// <summary>
        /// Returns the number of Peers in the list
        /// </summary>
        public int Count
        {
            get { return this.peerList.Count; }
        }


        /// <summary>
        /// Checks if the specified PeerConnectionID is in the list
        /// </summary>
        /// <param name="peer">The PeerConnectionID to search for</param>
        /// <returns>True if the PeerConnectionID was found, false otherwise</returns>
        public bool Contains(PeerConnectionID peer)
        {
            return (this.peerList.Contains(peer));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<PeerConnectionID>.Enumerator GetEnumerator()
        {
            return this.peerList.GetEnumerator();
        }
        #endregion
    }
}