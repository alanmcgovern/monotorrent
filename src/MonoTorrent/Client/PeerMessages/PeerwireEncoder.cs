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



using System;
using System.Net;
namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// Class used for decoding an IPeerMessage
    /// </summary>
    internal class PeerwireEncoder
    {
        /// <summary>
        /// Decodes a PeerMessage from the supplied peers recieve buffer
        /// </summary>
        /// <param name="id">The peer to decode a message from</param>
        /// <returns>The PeerMessage decoded from the recieve buffer</returns>
        public static IPeerMessageInternal Decode(ArraySegment<byte> buffer, int offset, int count, TorrentManager manager)
        {
            IPeerMessageInternal message;

            // The first byte tells us what kind of message it is
            switch (buffer.Array[buffer.Offset + offset])
            {
                case AllowedFastMessage.MessageId:
                    message = new AllowedFastMessage();
                    break;

                case BitfieldMessage.MessageId:
                    message = new BitfieldMessage(manager.Torrent.Pieces.Count);
                    break;

                case CancelMessage.MessageId:
                    message = new CancelMessage();
                    break;

                case ChokeMessage.MessageId:
                    message = new ChokeMessage();
                    break;

                case ExtendedListMessage.MessageId:
                    message = new ExtendedListMessage();
                    break;

                case HaveAllMessage.MessageId:
                    message = new HaveAllMessage();
                    break;

                case HaveMessage.MessageId:
                    message = new HaveMessage();
                    break;

                case HaveNoneMessage.MessageId:
                    message = new HaveNoneMessage();
                    break;

                case InterestedMessage.MessageId:
                    message = new InterestedMessage();
                    break;

                case NotInterestedMessage.MessageId:
                    message = new NotInterestedMessage();
                    break;

                case PieceMessage.MessageId:
                    message = new PieceMessage(manager.FileManager);
                    break;

                case PortMessage.MessageId:
                    message = new PortMessage();
                    break;

                case RejectRequestMessage.MessageId:
                    message = new RejectRequestMessage();
                    break;

                case RequestMessage.MessageId:
                    message = new RequestMessage();
                    break;

                case SuggestPieceMessage.MessageId:
                    message = new SuggestPieceMessage();
                    break;

                case UnchokeMessage.MessageId:
                    message = new UnchokeMessage();
                    break;


                case 21:                            // An "extended" message
                    switch (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.Array, buffer.Offset + (++offset))))
                    {
                        default:
                            throw new NotSupportedException();
                    }

                default:
                    message = new UnknownMessage();
                    break;
            }

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            message.Decode(buffer, offset + 1, count);
            return message;
        }
    }
}