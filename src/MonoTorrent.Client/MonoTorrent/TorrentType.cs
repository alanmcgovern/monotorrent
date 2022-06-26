//
// TorrentCreator.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006-2007 Gregor Burger and Alan McGovern
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

namespace MonoTorrent
{
    public enum TorrentType
    {
        /// <summary>
        /// Generates a V1 compatible torrent.
        /// </summary>
        V1Only,
        /// <summary>
        /// A torrent file with BitTorrent V1 metadata. All files are padded so they align to piece boundaries.
        /// </summary>
        V1OnlyWithPaddingFiles,
        /// <summary>
        /// A torrent file with BitTorrent V1 and BitTorrent V2 metadata
        /// </summary>
        V1V2Hybrid,
        /// <summary>
        /// A torrent file with BitTorrent V2 metadata
        /// </summary>
        V2Only,
    }
}
