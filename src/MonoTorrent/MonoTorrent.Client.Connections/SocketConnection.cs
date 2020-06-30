//
// SocketConnection.cs
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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent.Client.Connections
{
    public class SocketConnection : IConnection2
    {
        static readonly EventHandler<SocketAsyncEventArgs> Handler = HandleOperationCompleted;

        /// <summary>
        /// This stores a reusable 'SocketAsyncEventArgs' for every byte[] owned by ClientEngine.BufferPool
        /// </summary>
        static readonly Dictionary<byte[], SocketAsyncEventArgs> bufferCache = new Dictionary<byte[], SocketAsyncEventArgs> ();

        /// <summary>
        /// This stores reusable 'SocketAsyncEventArgs' for arbitrary byte[], or for when we are connecting
        /// to a peer and do not have a byte[] buffer to send/receive from.
        /// </summary>
        static readonly Queue<SocketAsyncEventArgs> otherCache = new Queue<SocketAsyncEventArgs> ();

        /// <summary>
        /// Where possible we will use a SocketAsyncEventArgs object which has already had
        /// 'SetBuffer(byte[],int,int)' invoked on it for the given byte[]. Reusing these is
        /// much more efficient than constantly calling SetBuffer on a different 'SocketAsyncEventArgs'
        /// object.
        /// </summary>
        /// <param name="buffer">The buffer we wish to get the reusuable 'SocketAsyncEventArgs' for</param>
        /// <returns></returns>
        static SocketAsyncEventArgs GetSocketAsyncEventArgs (byte[] buffer)
        {
            SocketAsyncEventArgs args;
            lock (bufferCache) {
                if (buffer != null && ClientEngine.BufferPool.Owns (buffer)) {
                    if (!bufferCache.TryGetValue (buffer, out args)) {
                        bufferCache[buffer] = args = new SocketAsyncEventArgs ();
                        args.SetBuffer (buffer, 0, buffer.Length);
                        args.Completed += Handler;
                    }
                } else {
                    if (otherCache.Count == 0) {
                        args = new SocketAsyncEventArgs ();
                        args.Completed += Handler;
                    } else {
                        args = otherCache.Dequeue ();
                    }

                    if (buffer != null)
                        args.SetBuffer (buffer, 0, buffer.Length);
                }
                return args;
            }
        }

        #region Member Variables

        public byte[] AddressBytes => EndPoint.Address.GetAddressBytes ();

        public bool CanReconnect => !IsIncoming;

        public bool Connected => Socket.Connected;

        bool Disposed { get; set; }

        EndPoint IConnection.EndPoint => EndPoint;

        public IPEndPoint EndPoint { get; }

        public bool IsIncoming { get; }

        ReusableTaskCompletionSource<int> ReceiveTcs { get; }

        ReusableTaskCompletionSource<int> SendTcs { get; }

        Socket Socket { get; set; }

        public Uri Uri { get; protected set; }

        #endregion


        #region Constructors

        protected SocketConnection (Uri uri)
            : this (new Socket ((uri.Scheme == "ipv4") ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp),
                  new IPEndPoint (IPAddress.Parse (uri.Host), uri.Port), false)

        {
            Uri = uri;
        }

        protected SocketConnection (Socket socket, bool isIncoming)
            : this (socket, (IPEndPoint) socket.RemoteEndPoint, isIncoming)
        {

        }

        SocketConnection (Socket socket, IPEndPoint endpoint, bool isIncoming)
        {
            ReceiveTcs = new ReusableTaskCompletionSource<int> ();
            SendTcs = new ReusableTaskCompletionSource<int> ();
            Socket = socket;
            EndPoint = endpoint;
            IsIncoming = isIncoming;
        }

        static readonly HashSet<ReusableTaskCompletionSource<int>> hasBeenSeenAlready = new HashSet<ReusableTaskCompletionSource<int>> ();
        static async void HandleOperationCompleted (object sender, SocketAsyncEventArgs e)
        {
            // Don't retain the TCS forever. Note we do not want to null out the byte[] buffer
            // as we *do* want to retain that so that we can avoid the expensive SetBuffer calls.
            var tcs = e.UserToken as ReusableTaskCompletionSource<int>;
            if (tcs == null) {
                var tuple = (Tuple<Task, ReusableTaskCompletionSource<int>>)e.UserToken;
                await tuple.Item1;
                tcs = tuple.Item2;
            }
            SocketError error = e.SocketError;
            int transferred = e.BytesTransferred;
            e.RemoteEndPoint = null;
            e.UserToken = null;

            if (e.Buffer == null)
                lock (hasBeenSeenAlready)
                    if (!hasBeenSeenAlready.Add (tcs)) {
                        throw new Exception ("The completion delegate has been invoked twice");
            }

            // If the 'SocketAsyncEventArgs' was used to connect, or if it was using a buffer
            // *not* managed by our BufferPool, then we should put it back in the 'other' cache.
            if (e.Buffer == null || !ClientEngine.BufferPool.Owns (e.Buffer)) {
                lock (bufferCache) {
                    if (e.Buffer != null)
                        e.SetBuffer (null, 0, 0);
                    otherCache.Enqueue (e);
                }
            }

            if (error != SocketError.Success)
                tcs.SetException (new SocketException ((int) error));
            else
                tcs.SetResult (transferred);
        }

        #endregion


        #region Async Methods

        async Task IConnection.ConnectAsync ()
        {
            await ConnectAsync ();
        }

        public async ReusableTask ConnectAsync ()
        {
            var tcs = new ReusableTaskCompletionSource<int> ();
            SocketAsyncEventArgs args = GetSocketAsyncEventArgs (null);
            args.RemoteEndPoint = EndPoint;
            args.UserToken = tcs;

            if (!Socket.ConnectAsync (args))
                tcs.SetResult (0);

            await tcs.Task;
        }

        async Task<int> IConnection.ReceiveAsync (byte[] buffer, int offset, int count)
        {
            return await ReceiveAsync (buffer, offset, count);
        }

        public static void ClearStacktraces ()
        {
            lock (doubleReceiveStacktraces)
                doubleReceiveStacktraces.Clear ();
            lock (doubleSendStacktraces)
                doubleSendStacktraces.Clear ();
        }

        int receiving = 0;
        static readonly List<string> doubleReceiveStacktraces = new List<string> ();
        public static string[] DoubleReceiveStacktraces {
            get {
                lock (doubleReceiveStacktraces)
                    return doubleReceiveStacktraces.ToArray ();
            }
        }
        public ReusableTask<int> ReceiveAsync (byte[] buffer, int offset, int count)
            => ReceiveAsync (buffer, offset, count, null);

        internal async ReusableTask<int> ReceiveAsync (byte[] buffer, int offset, int count, Task delayCompletion)

        {
            if (Interlocked.Increment (ref receiving) != 1) {
                lock (doubleReceiveStacktraces)
                    doubleReceiveStacktraces.Add ($"Inc - Disposed: {Disposed} - {Environment.StackTrace}");
                Socket.SafeDispose ();
            }

            try {
                SocketAsyncEventArgs args = GetSocketAsyncEventArgs (buffer);
                args.SetBuffer (offset, count);
                if (delayCompletion == null)
                    args.UserToken = ReceiveTcs;
                else
                    args.UserToken = Tuple.Create (delayCompletion, ReceiveTcs);

#if ALLOW_EXECUTION_CONTEXT_SUPPRESSION
            AsyncFlowControl? control = null;
            if (!ExecutionContext.IsFlowSuppressed ())
                control = ExecutionContext.SuppressFlow ();
#endif

                try {
                    if (!Socket.ReceiveAsync (args))
                        ReceiveTcs.SetResult (args.BytesTransferred);
                } finally {
#if ALLOW_EXECUTION_CONTEXT_SUPPRESSION
                control?.Undo ();
#endif
                }

                return await ReceiveTcs.Task;
            } finally {
                if (Interlocked.Decrement (ref receiving) != 0)
                    lock (doubleReceiveStacktraces)
                        doubleReceiveStacktraces.Add ($"Dec - Disposed: {Disposed} - {Environment.StackTrace}");

            }
        }

        async Task<int> IConnection.SendAsync (byte[] buffer, int offset, int count)
        {
            return await SendAsync (buffer, offset, count);
        }

        int sending = 0;
        internal static readonly List<string> doubleSendStacktraces = new List<string> ();
        public static string[] DoubleSendStacktraces {
            get {
                lock (doubleSendStacktraces)
                    return doubleSendStacktraces.ToArray ();
            }
        }

        bool peerIOSend;
        bool networkIOSend;

        public bool PeerIOSend {
            get { return peerIOSend; }
            set {
                if (value && peerIOSend)
                    lock (doubleSendStacktraces)
                        doubleSendStacktraces.Add (Environment.StackTrace);
                if (!value && !peerIOSend)
                    lock (doubleSendStacktraces)
                        doubleSendStacktraces.Add (Environment.StackTrace);
                peerIOSend = value;
            }
        }
        public object PeerIOLocker { get; } = new object ();
        public bool NetworkIOSend {
            get { return networkIOSend; }
            set {
                if (value && networkIOSend)
                    lock (doubleSendStacktraces)
                        doubleSendStacktraces.Add (Environment.StackTrace);
                if (!value && !networkIOSend)
                    lock (doubleSendStacktraces)
                        doubleSendStacktraces.Add (Environment.StackTrace);
                networkIOSend = value;
            }
        }
        public object NetworkIOLocker { get; } = new object ();

        public ReusableTask<int> SendAsync (byte[] buffer, int offset, int count)
            => SendAsync (buffer, offset, count, null);

        internal async ReusableTask<int> SendAsync (byte[] buffer, int offset, int count, Task delayCompletion)
        {
            if (Interlocked.Increment (ref sending) != 1) {
                lock (doubleSendStacktraces)
                    doubleSendStacktraces.Add ($"Inc - Disposed: {Disposed} - {Environment.StackTrace}");
                Socket?.SafeDispose ();
            }

            try {
                SocketAsyncEventArgs args = GetSocketAsyncEventArgs (buffer);
                args.SetBuffer (offset, count);
                if (delayCompletion == null)
                    args.UserToken = SendTcs;
                else
                    args.UserToken = Tuple.Create (delayCompletion, SendTcs);

#if ALLOW_EXECUTION_CONTEXT_SUPPRESSION
            AsyncFlowControl? control = null;
            if (!ExecutionContext.IsFlowSuppressed ())
                control = ExecutionContext.SuppressFlow ();
#endif

                try {
                    if (!Socket.SendAsync (args)) {
                        if (delayCompletion != null)
                            await delayCompletion;
                        SendTcs.SetResult (count);
                    }
                } finally {
#if ALLOW_EXECUTION_CONTEXT_SUPPRESSION
                control?.Undo ();
#endif
                }

                return await SendTcs.Task;
            } finally {
                if (Interlocked.Decrement (ref sending) != 0)
                    lock (doubleSendStacktraces)
                        doubleSendStacktraces.Add ($"Dec - Disposed: {Disposed} - {Environment.StackTrace}");
            }
        }

        public void Dispose ()
        {
            Disposed = true;
            Socket?.SafeDispose ();
        }

#endregion
    }
}