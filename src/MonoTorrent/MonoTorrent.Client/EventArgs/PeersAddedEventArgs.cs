//
// PeersAddedEventArgs.cs
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


namespace MonoTorrent.Client
{
    public abstract class PeersAddedEventArgs : TorrentEventArgs
    {
        /// <summary>
        /// The number of peers which were already known.
        /// </summary>
        public int ExistingPeers { get; }

        /// <summary>
        /// The number of new peers which were added.
        /// </summary>
        public int NewPeers { get; }

        /// <summary>
        /// Creates a new PeersAddedEventArgs
        /// </summary>
        /// <param name="manager">The <see cref="TorrentManager"/> which peers were discovered for.</param>
        /// <param name="peersAdded">The number of peers just added. This will be less than <paramref name="total"/> if some peers are duplicates.</param>
        /// <param name="total">The total number of peers discovered, including duplicates.</param>
        protected PeersAddedEventArgs (TorrentManager manager, int peersAdded, int total)
            : base (manager)
        {
            NewPeers = peersAdded;
            ExistingPeers = total - peersAdded;
        }
    }
}
