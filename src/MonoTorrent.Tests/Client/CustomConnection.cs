using System;
using System.Net;
using System.Net.Sockets;
using MonoTorrent.Client.Connections;

namespace MonoTorrent.Tests.Client
{
    public class CustomConnection : IConnection
    {
        private readonly Socket s;
        public string Name;

        public CustomConnection(Socket s, bool incoming)
        {
            this.s = s;
            IsIncoming = incoming;
        }

        public int? ManualBytesReceived { get; set; }

        public int? ManualBytesSent { get; set; }

        public bool SlowConnection { get; set; }

        public byte[] AddressBytes
        {
            get { return ((IPEndPoint) s.RemoteEndPoint).Address.GetAddressBytes(); }
        }

        public bool Connected
        {
            get { return s.Connected; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool IsIncoming { get; }

        public EndPoint EndPoint
        {
            get { return s.RemoteEndPoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            throw new InvalidOperationException();
        }

        public void EndConnect(IAsyncResult result)
        {
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginReceiveStarted != null)
                BeginReceiveStarted(this, EventArgs.Empty);
            if (SlowConnection)
                count = 1;
            return s.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            if (EndReceiveStarted != null)
                EndReceiveStarted(null, EventArgs.Empty);

            if (ManualBytesReceived.HasValue)
                return ManualBytesReceived.Value;

            try
            {
                return s.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginSendStarted != null)
                BeginSendStarted(null, EventArgs.Empty);

            if (SlowConnection)
                count = 1;
            return s.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            if (EndSendStarted != null)
                EndSendStarted(null, EventArgs.Empty);

            if (ManualBytesSent.HasValue)
                return ManualBytesSent.Value;

            try
            {
                return s.EndSend(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        //private bool disposed;
        public void Dispose()
        {
            // disposed = true;
            s.Close();
        }

        public Uri Uri
        {
            get { return new Uri("tcp://127.0.0.1:1234"); }
        }

        public event EventHandler BeginReceiveStarted;
        public event EventHandler EndReceiveStarted;

        public event EventHandler BeginSendStarted;
        public event EventHandler EndSendStarted;

        public override string ToString()
        {
            return Name;
        }


        public int Receive(byte[] buffer, int offset, int count)
        {
            var r = BeginReceive(buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4)))
                throw new Exception("Could not receive required data");
            return EndReceive(r);
        }

        public int Send(byte[] buffer, int offset, int count)
        {
            var r = BeginSend(buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4)))
                throw new Exception("Could not receive required data");
            return EndSend(r);
        }
    }
}