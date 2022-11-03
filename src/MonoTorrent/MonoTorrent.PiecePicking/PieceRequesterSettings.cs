//
// StandardPieceRequester.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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

namespace MonoTorrent.PiecePicking
{
    public class PieceRequesterSettings
    {
        public static PieceRequesterSettings Default { get; } = new PieceRequesterSettings ();

        /// <summary>
        /// When set to false, <see cref="ITorrentManagerFile.Priority"/> is not taken into account when choosing pieces. Files marked as 'DoNotDownload' will be downloaded.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowPrioritisation { get; }

        /// <summary>
        /// When set to false, pieces will be selected sequentially. If <see cref="AllowRarestFirst"/> is enabled, then set of pieces which will be available to choose from will be reduced to the 'rarest' set first,
        /// and then the picker will choose sequentially from that subset. If you need true linear picking, you must disable <see cref="AllowRandomised"/> as well as <see cref="AllowRarestFirst"/>.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowRandomised { get; }


        /// <summary>
        /// When set to false, the rarest subset of pieces will not be computed.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowRarestFirst { get; }

        /// <summary>
        /// The maximum number of concurrent requests which will be issued for the same block of data during 'end game' mode.
        /// A higher value can decrease the time it takes for the final few pieces to be received at the cost of downloading
        /// excess data. Blocks which are not received within a reasonable amount of time will be re-requested from another peer.
        /// Defaults to 2.
        /// </summary>
        public int MaximumDuplicateRequests { get; }

        public PieceRequesterSettings (
            bool allowPrioritisation = true,
            bool allowRandomised = true,
            bool allowRarestFirst = true,
            int maximumDuplicateRequests = 2)
            => (AllowPrioritisation, AllowRandomised, AllowRarestFirst, MaximumDuplicateRequests) = (allowPrioritisation, allowRandomised, allowRarestFirst, maximumDuplicateRequests);
    }
}
