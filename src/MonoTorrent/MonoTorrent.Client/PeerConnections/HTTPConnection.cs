//
// HTTPConnection.cs
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
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;

namespace MonoTorrent.Client.Connections
{
    internal class HttpConnection : IConnection
    {
        #region Member Variables

        public bool CanReconnect
        {
            get { return true; }
        }

        public bool Connected
        {
            get { return true; }
        }

        EndPoint IConnection.EndPoint
        {
            get { return null; }
        }

        public bool IsIncoming
        {
            get { return false; }
        }

        private Queue<byte[]> buffers;
        private int bytesReceived;
        private int bytesSent;
        private const int BUFFER_SIZE = 2000;

        #endregion

        public HttpConnection()
        {
            buffers = new Queue<byte[]>();
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            AsyncResult result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            byte[] srcBuffer = buffers.Dequeue();
            Array.Copy(srcBuffer, 0, buffer, offset, count);

            bytesReceived = srcBuffer.Length;
            AsyncResult result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            bytesSent = buffer.Length;
            PeerIdInternal id = (PeerIdInternal)state;
            PeerMessage m = PeerMessage.DecodeMessage(buffer, offset, count, id.TorrentManager);
            if (m is RequestMessage)
                SendHttpRequest((RequestMessage)m, callback, id);
            //For other message do nothing

            AsyncResult result = new AsyncResult(callback, state);
            result.Complete();
            return result;
        }

        public void EndConnect(IAsyncResult result)
        {
            //do nothing
        }

        public int EndReceive(IAsyncResult result)
        {
            return bytesReceived;
        }

        public int EndSend(IAsyncResult result)
        {
            return bytesSent;
        }

        public void Dispose()
        {
            //do nothing
        }

        public byte[] AddressBytes
        {
            get { return new byte[4]; }
        }

        private void SendHttpRequest(RequestMessage msg, AsyncCallback callback, PeerIdInternal id)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(id.Peer.ConnectionUri.Host);
            builder.Append(id.TorrentManager.Torrent.Name);
            //TODO multi file support
            //builder.Append ("/");
            //builder.Append (torrentManager.Torrent.Files[i].Path);

            WebRequest request = WebRequest.Create(builder.ToString());
            if (!(request is HttpWebRequest))
                throw new Exception("bad url format!");
            int pieceOffset = msg.PieceIndex * id.TorrentManager.FileManager.PieceLength + msg.StartOffset;
            ((HttpWebRequest)request).AddRange(pieceOffset, pieceOffset + msg.RequestLength);
            WebResponse response = request.GetResponse();

            if (((HttpWebResponse)response).StatusCode != HttpStatusCode.OK)
                return;

            byte[] buff;
            using (Stream stream = response.GetResponseStream())
            {
                buff = new byte[stream.Length];
                stream.Read(buff, 0, (int)stream.Length);
            }

            //cut in 2k buffer message
            for (int i = 0; i < buff.Length; i += BUFFER_SIZE)
                buffers.Enqueue(CreatePieceMessage(id.TorrentManager, buff, msg.PieceIndex, msg.StartOffset, i));
        }

        private byte[] CreatePieceMessage(TorrentManager torrentManager, byte[] fileBuffer, int pieceIndex, int startOffset, int index)
        {
            //PieceMessage m = new PieceMessage (torrentManager, pieceIndex, startOffset, msg.Count);
            //m.Encode (buffer, offset);
            //buffer is return buffer here whereas want to put the piece buffer

            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            int len = Math.Min(fileBuffer.Length - index, BUFFER_SIZE);
            writer.Write(len + 9);//calcul size but not sure if must include size itself?
            writer.Write(0x07);//byte messageid
            writer.Write(pieceIndex);//int 
            writer.Write(startOffset);//int

            writer.Write(fileBuffer, index, len);
            return ((MemoryStream)writer.BaseStream).ToArray();
        }
    }
}
