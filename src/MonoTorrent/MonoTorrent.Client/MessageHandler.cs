using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    internal static class MessageHandler 
    {
        private struct AsyncMessageDetails
        {
            public ArraySegment<byte> Buffer;
            public int StartOffset;
            public int Count;
        }

        internal static void EnqueueReceived(PeerId id, ArraySegment<byte> buffer, int startOffset, int count)
        {
            ArraySegment<byte> messageBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref messageBuffer, buffer.Count);
            Buffer.BlockCopy(buffer.Array, buffer.Offset + startOffset, messageBuffer.Array, messageBuffer.Offset, count);

            AsyncMessageDetails details = new AsyncMessageDetails();
            details.Buffer = messageBuffer;
            details.Count = count;
            details.StartOffset = 0;

            HandleMessage(id, details);
        }

        internal static void EnqueueSend(PeerId id)
        {
            if (id.Connection != null)
                id.ConnectionManager.ProcessQueue(id);
        }

        internal static void EnqueueCleanup(PeerId id)
        {
            ClientEngine.MainLoop.Queue(delegate {
                id.ConnectionManager.AsyncCleanupSocket(id, true, id.DisconnectReason);
            });
        }

        private static void HandleMessage(PeerId id, AsyncMessageDetails messageDetails)
        {
            try
            {
                if (id.Connection == null)
                    return;

                try
                {
                    PeerMessage message = PeerMessage.DecodeMessage(messageDetails.Buffer, 0, messageDetails.Count, id.TorrentManager);

                    // Fire the event to say we recieved a new message
                    PeerMessageEventArgs e = new PeerMessageEventArgs(id.TorrentManager, (PeerMessage)message, MonoTorrent.Common.Direction.Incoming, id);
                    id.ConnectionManager.RaisePeerMessageTransferred(e);

                    message.Handle(id);
                }
                catch (Exception ex)
                {
                    // Should i nuke the peer with the dodgy message too?
                    Logger.Log(null, "*CRITICAL EXCEPTION* - Error decoding message: {0}", ex);
                }
            }
            finally
            {
                ClientEngine.BufferManager.FreeBuffer(ref messageDetails.Buffer);
            }
        }
    }
}
