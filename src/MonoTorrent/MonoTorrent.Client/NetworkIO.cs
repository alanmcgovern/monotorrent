//
// NetworkIO.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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
using MonoTorrent.Client;
using MonoTorrent.Common;
using System.Threading;
using MonoTorrent.Client.Messages.Standard;
using System.Net.Sockets;

namespace MonoTorrent.Client
{
    internal static class NetworkIO
    {
        private static RateLimiter uploadLimiter;        // Contains the logic to decide how many chunks we can download

        private static List<IAsyncResult> runningReceives;
        private static List<IAsyncResult> runningSends;

        private static MonoTorrentCollection<PeerIdInternal> receive;
        private static MonoTorrentCollection<PeerIdInternal> send;

        static NetworkIO()
        {
            send = new MonoTorrentCollection<PeerIdInternal>();
            receive = new MonoTorrentCollection<PeerIdInternal>();
            runningReceives = new List<IAsyncResult>();
            runningSends = new List<IAsyncResult>();

            ThreadPool.QueueUserWorkItem(delegate
            {
                while (true)
                {
                    lock (receive)
                        if (receive.Count > 0)
                        {
                            IAsyncResult r = StartReceive(receive.Dequeue());
                            if (r != null)
                                runningReceives.Add(r);
                        }
                    lock (send)
                        if (send.Count > 0)
                        {
                            IAsyncResult r = StartSend(send.Dequeue());
                            if (r != null)
                                runningSends.Add(r);
                        }
                    for (int i = 0; i < runningReceives.Count; i++)
                    {
                        if (!runningReceives[i].IsCompleted)
                            continue;

                        CompleteReceive(runningReceives[i]);
                        break;
                    }

                    for (int i = 0; i < runningSends.Count; i++)
                    {
                        if (!runningSends[i].IsCompleted)
                            continue;

                        CompleteSend(runningSends[i]);
                        break;
                    }

                    System.Threading.Thread.Sleep(1);
                }
            });
        }

        private static void CompleteSend(IAsyncResult result)
        {
            runningSends.Remove(result);

            bool cleanup = false;
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

            try
            {
                int sent = id.Connection.EndSend(result);
                if (sent == 0)
                {
                    cleanup = true;
                    return;
                }

                TransferType type = (id.Connection.CurrentlySendingMessage is PieceMessage) ? TransferType.Data : TransferType.Protocol;
                id.Connection.SentBytes(sent, type);
                id.TorrentManager.Monitor.BytesSent(sent, type);

                // If we havn't sent everything, send the rest of the data
                if (id.Connection.BytesSent != id.Connection.BytesToSend)
                {
                    lock (send)
                        send.Add(id);
                }
                else
                {
                    id.Connection.MessageSentCallback(id);
                }
            }
            catch (Exception ex)
            {
                cleanup = true;
            }
            finally
            {
                if (cleanup)
                    id.ConnectionManager.CleanupSocket(id, "");
            }
        }

        private static void CompleteReceive(IAsyncResult result)
        {
            runningReceives.Remove(result);

            bool cleanUp = false;
            PeerIdInternal id = (PeerIdInternal)result.AsyncState;

            try
            {
                // If we receive 0 bytes, the connection has been closed, so exit
                int bytesReceived = id.Connection.EndReceive(result);
                if (bytesReceived == 0)
                {
                    cleanUp = true;
                    return;
                }

                // If the first byte is '7' and we're receiving more than 256 bytes (a made up number)
                // then this is a piece message, so we add it as "data", not protocol. 256 bytes should filter out
                // any non piece messages that happen to have '7' as the first byte.
                TransferType type = (id.Connection.recieveBuffer.Array[id.Connection.recieveBuffer.Offset] == PieceMessage.MessageId && id.Connection.BytesToRecieve > 256) ? TransferType.Data : TransferType.Protocol;
                id.Connection.ReceivedBytes(bytesReceived, type);
                id.TorrentManager.Monitor.BytesReceived(bytesReceived, type);

                // If we don't have the entire message, recieve the rest
                if (id.Connection.BytesReceived < id.Connection.BytesToRecieve)
                {
                    lock (receive)
                        receive.Add(id);
                }
                else
                {
                    // Invoke the callback we were told to invoke once the message had been received fully
                    id.Connection.MessageReceivedCallback(id);
                }
            }

            catch (Exception ex)
            {
                cleanUp = true;
            }
            finally
            {
                if (cleanUp)
                    id.ConnectionManager.CleanupSocket(id, "");
            }
        }

        private static IAsyncResult StartSend(PeerIdInternal id)
        {
            try
            {
                return id.Connection.BeginSend(id.Connection.sendBuffer, id.Connection.BytesSent, id.Connection.BytesToSend - id.Connection.BytesSent, SocketFlags.None, null, id);
            }
            catch
            {
                return null;
            }
        }

        private static IAsyncResult StartReceive(PeerIdInternal id)
        {
            try
            {
                return id.Connection.BeginReceive(id.Connection.recieveBuffer, id.Connection.BytesReceived, id.Connection.BytesToRecieve - id.Connection.BytesReceived, SocketFlags.None, null, id);
            }
            catch
            {
                return null;
            }
        }


        public static void EnqueueReceive(PeerIdInternal id)
        {
            lock (receive)
                receive.Add(id);
        }

        public static void EnqueueSend(PeerIdInternal id)
        {
            lock (send)
                send.Add(id);
        }
    }
}
