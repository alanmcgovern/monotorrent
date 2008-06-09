using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client
{
    internal class MessageHandler 
    {
        private struct AsyncMessageDetails
        {
            public ArraySegment<byte> Buffer;
            public int StartOffset;
            public int Count;
        }

        #region Member Variables

        private Queue<PeerIdInternal> cleanUpQueue = new Queue<PeerIdInternal>();
        private bool messageLoopActive;
        private Thread messageLoopThread;
        private readonly object queuelock = new object();
        private Queue<Nullable<KeyValuePair<PeerIdInternal, AsyncMessageDetails>>> queue;
        private Queue<PeerIdInternal> sendQueue;
        private ManualResetEvent waitHandle;

        #endregion Member Variables


        #region Fields
        
        public bool IsActive
        {
            get { return this.messageLoopThread != null; }
        }

        #endregion


        #region Constructor

        public MessageHandler()
        {
            this.queue = new Queue<KeyValuePair<PeerIdInternal, AsyncMessageDetails>?>();
            this.sendQueue = new Queue<PeerIdInternal>();
            this.waitHandle = new ManualResetEvent(false);
        }

        #endregion


        #region Internal Methods

        internal void EnqueueReceived(PeerIdInternal id, ArraySegment<byte> buffer, int startOffset, int count)
        {
            ArraySegment<byte> messageBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref messageBuffer, buffer.Count);
            Buffer.BlockCopy(buffer.Array, buffer.Offset + startOffset, messageBuffer.Array, messageBuffer.Offset, count);

            AsyncMessageDetails details = new AsyncMessageDetails();
            details.Buffer = messageBuffer;
            details.Count = count;
            details.StartOffset = 0;

            MainLoop.Queue(delegate {
                HandleMessage(id, details);
            });
        }

        internal void EnqueueSend(PeerIdInternal id)
        {
            MainLoop.Queue(delegate {
                if (id.Connection != null)
                    id.ConnectionManager.ProcessQueue(id);
            });
        }

        internal void EnqueueCleanup(PeerIdInternal id)
        {
            ConnectionManager manager = id.ConnectionManager;
            MainLoop.Queue(delegate {
                manager.AsyncCleanupSocket(id, true, id.DisconnectReason);
            });
        }

        #endregion Internal Methods


        #region Private Methods

        private void HandleMessage(PeerIdInternal id, AsyncMessageDetails messageDetails)
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

        #endregion Private Methods
    }
}
