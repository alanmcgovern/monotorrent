//
// PeerwireEncoder.cs
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
    /// Class used for decoding an IPeerMessage
    /// </summary>
    public class PeerwireEncoder
    {
        /// <summary>
        /// Decodes a PeerMessage from the supplied peers recieve buffer
        /// </summary>
        /// <param name="id">The peer to decode a message from</param>
        /// <returns>The PeerMessage decoded from the recieve buffer</returns>
        public static IPeerMessage Decode(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            IPeerMessage message = null;

            // The first byte tells us what kind of message it is
            switch (buffer[offset])
            {
                case 0:
                    message = new ChokeMessage();
                    break;
                case 1:
                    message = new UnchokeMessage();
                    break;
                case 2:
                    message = new InterestedMessage();
                    break;
                case 3:
                    message = new NotInterestedMessage();
                    break;
                case 4:
                    message = new HaveMessage();
                    break;
                case 5:
                    message = new BitfieldMessage(manager.Torrent.Pieces.Length);
                    break;
                case 6:
                    message = new RequestMessage();
                    break;
                case 7:
                    message = new PieceMessage(manager.DiskManager);
                    break;
                case 8:
                    message = new CancelMessage();
                    break;
                case 9:
                    message = new PortMessage();
                    break;
                default:
#warning LOG THESE
                    return null;        
            }

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes recieved. If the message isn't complete, throw an exception
            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}