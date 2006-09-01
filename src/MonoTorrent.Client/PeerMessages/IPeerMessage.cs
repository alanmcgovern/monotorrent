//
// IPeerMessage.cs
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



namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// Common interface for all PeerMessages
    /// </summary>
    public interface IPeerMessage
    {
        /// <summary>
        /// Encodes the PeerMessage into the supplied buffer
        /// </summary>
        /// <param name="id">The peer who we are about to send the message to</param>
        /// <param name="buffer">The buffer to encode the message to</param>
        /// <param name="offset">The offset at which to start encoding the data to</param>
        /// <returns>The number of bytes encoded into the buffer</returns>
        int Encode(byte[] buffer, int offset);


        /// <summary>
        /// Decodes a peer message from the supplied buffer
        /// </summary>
        /// <param name="id">The peer to decode the message from</param>
        /// <param name="buffer">The buffer to decode the message from</param>
        /// <param name="offset">The offset thats the message starts at</param>
        /// <param name="length">The maximum number of bytes to read from the buffer</param>
        void Decode(byte[] buffer, int offset, int length);


        /// <summary>
        /// Performs any necessary actions required to process the message
        /// </summary>
        /// <param name="id">The Peer who's message will be handled</param>
        void Handle(PeerConnectionID id);


        /// <summary>
        /// Returns the length of the message in bytes
        /// </summary>
        int ByteLength { get; }
    }
}