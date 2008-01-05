using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Client
{
    internal class MessageHandler : IDisposable
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

        void IDisposable.Dispose()
        {
            Dispose();
        }

        internal void Dispose()
        {
            if (!IsActive)
                return;

            this.Stop();
        }

        internal void EnqueueReceived(PeerIdInternal id, ArraySegment<byte> buffer, int startOffset, int count)
        {
            ArraySegment<byte> messageBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref messageBuffer, buffer.Count);
            Buffer.BlockCopy(buffer.Array, buffer.Offset + startOffset, messageBuffer.Array, messageBuffer.Offset, count);

            AsyncMessageDetails details = new AsyncMessageDetails();
            details.Buffer = messageBuffer;
            details.Count = count;
            details.StartOffset = 0;

            lock (this.queuelock)
            {
                this.queue.Enqueue(new KeyValuePair<PeerIdInternal, AsyncMessageDetails>(id, details));
                this.waitHandle.Set();
            }
        }

        internal void EnqueueSend(PeerIdInternal id)
        {
            lock (this.queuelock)
            {
                this.sendQueue.Enqueue(id);
                this.waitHandle.Set();
            }
        }

        internal void EnqueueCleanup(PeerIdInternal id)
        {
            lock (this.queuelock)
            {
                this.cleanUpQueue.Enqueue(id);
                this.waitHandle.Set();
            }
        }

        internal void Start()
        {
            if (IsActive)
                throw new InvalidOperationException("Message loop already started");

            this.messageLoopActive = true;
            this.waitHandle.Reset();
            this.messageLoopThread = new Thread(new ThreadStart(MessageLoop));
            this.messageLoopThread.Start();
        }

        internal void Stop()
        {
            if (!IsActive)
                throw new InvalidOperationException("Message loop is not running");

            this.messageLoopActive = false;
            this.waitHandle.Set();
            this.messageLoopThread.Join(500);
            this.messageLoopThread = null;
        }

        #endregion Internal Methods


        #region Private Methods

        private void HandleMessage(PeerIdInternal id, AsyncMessageDetails messageDetails)
        {
            try
            {
                lock (id.TorrentManager.listLock)
                {
                    lock (id)
                    {
                        if (id.Connection == null)
                            return;

                        try
                        {
                            IPeerMessageInternal message = PeerwireEncoder.Decode(messageDetails.Buffer, 0, messageDetails.Count, id.TorrentManager);

                            // Fire the event to say we recieved a new message
                            PeerMessageEventArgs e = new PeerMessageEventArgs(id.TorrentManager, (IPeerMessage)message, MonoTorrent.Common.Direction.Incoming, id);
                            id.ConnectionManager.RaisePeerMessageTransferred(e);

                            message.Handle(id);
                        }
                        catch (Exception)
                        {
                            //FIXME: #warning Do this in a better way so as to not hide an important exception!
                            return;
                        }
                    }
                }
            }
            finally
            {
                ClientEngine.BufferManager.FreeBuffer(ref messageDetails.Buffer);
            }
        }

        private void MessageLoop()
        {
            PeerIdInternal sendMessageToId;
            PeerIdInternal cleanupId;
            Nullable<KeyValuePair<PeerIdInternal, AsyncMessageDetails>> receivedMessage;

            while (this.messageLoopActive)
            {
                receivedMessage = null;
                cleanupId = null;
                sendMessageToId = null;

                lock (this.queuelock)
                {
                    if (this.queue.Count > 0)
                        receivedMessage = this.queue.Dequeue();

                    if (this.sendQueue.Count > 0)
                        sendMessageToId = this.sendQueue.Dequeue();

                    if (this.cleanUpQueue.Count > 0)
                        cleanupId = this.cleanUpQueue.Dequeue();

                    if (this.queue.Count == 0 && this.sendQueue.Count == 0 && this.cleanUpQueue.Count == 0)
                        this.waitHandle.Reset();
                }

                if (receivedMessage.HasValue)
                    HandleMessage(receivedMessage.Value.Key, receivedMessage.Value.Value);

                if (sendMessageToId != null)
                    SendMessage(sendMessageToId);

                if (cleanupId != null)
                    cleanupId.ConnectionManager.AsyncCleanupSocket(cleanupId, true, "Async Cleanup");

                this.waitHandle.WaitOne();
            }
        }

        private void SendMessage(PeerIdInternal id)
        {
            lock (id.TorrentManager.listLock)
                lock (id)
                    if (id.Connection != null)
                        id.ConnectionManager.ProcessQueue(id);
        }

        #endregion Private Methods
    }
}
