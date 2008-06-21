//
// MessageLoop.cs
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
using MonoTorrent.Dht.Messages;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Listeners;

namespace MonoTorrent.Dht
{
    public class MessageLoop
    {
        List<IAsyncResult> activeSends = new List<IAsyncResult>();
        IListener listener;
        private object locker = new object();
        Queue<KeyValuePair<Message, IPEndPoint>> sendQueue = new Queue<KeyValuePair<Message, IPEndPoint>>();
        Queue<KeyValuePair<IPEndPoint, Message>> receiveQueue = new Queue<KeyValuePair<IPEndPoint, Message>>();
        Thread thread;
        ManualResetEvent waitHandle = new ManualResetEvent(false);

        private bool CanSend
        {
            get { return activeSends.Count < 5 && sendQueue.Count > 0; }
        }

        public MessageLoop(IListener listener)
        {
            this.listener = listener;
            listener.MessageReceived += new MessageReceived(MessageReceived);
            thread = new Thread(Loop);
            thread.IsBackground = true;

            thread.Start();
            if (!listener.Started)
                listener.Start();
        }

        void MessageReceived(Message m, IPEndPoint endpoint)
        {
            lock (locker)
            {
                receiveQueue.Enqueue(new KeyValuePair<IPEndPoint, Message>(endpoint, m));
                waitHandle.Set();
            }
        }



        void Loop()
        {
            while (true)
            {
                KeyValuePair<IPEndPoint, Message>? receive = null;
                KeyValuePair<Message, IPEndPoint>? send = null;

                lock (locker)
                {
                    if (CanSend)
                        send = sendQueue.Dequeue();

                    if (receiveQueue.Count > 0)
                        receive = receiveQueue.Dequeue();

                    if (receiveQueue.Count == 0 && !CanSend)
                        waitHandle.Reset();
                }

                if (send != null)
                    SendMessage(send.Value);

                if (receive != null)
                    Handle(receive.Value.Key, receive.Value.Value);

                waitHandle.WaitOne();
            }
        }

        private void Handle(IPEndPoint endpoint, Message message)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void SendMessage(KeyValuePair<Message, IPEndPoint> keypair)
        {
            byte[] send = keypair.Key.Encode();
            listener.Send(send, keypair.Value);
        }

        internal void EnqueueSend(Message message, IPEndPoint endpoint)
        {
            lock (locker)
            {
                sendQueue.Enqueue(new KeyValuePair<Message, IPEndPoint>(message, endpoint));
                waitHandle.Set();
            }
        }

        internal bool ReceivedResponse(BEncodedString bEncodedString)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}

