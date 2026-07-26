//
// SocketConnectionTests.cs
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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Utp;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class UtpConnectionTests
    {
        UtpPeerConnection Incoming;
        UtpPeerConnectionListener IncomingListener;
        UtpPeerConnection Outgoing;
        UtpPeerConnectionListener OutgoingListener;
        static readonly object UcatOutputLocker = new object ();

        async Task SetupPeerPair ()
        {
            var tcs = new TaskCompletionSource<UtpPeerConnection> ();
            IncomingListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            IncomingListener.Start ();

            OutgoingListener = new UtpPeerConnectionListener (new IPEndPoint (IPAddress.Loopback, 0));
            OutgoingListener.Start ();

            Outgoing = new UtpPeerConnection (OutgoingListener, OutgoingListener.SendQueue, IncomingListener.LocalEndPoint, 123);
            IncomingListener.ConnectionReceived += (o, e) => {
                tcs.SetResult ((UtpPeerConnection) e.Connection);
            };

            await Outgoing.ConnectAsync ();
            Incoming = await tcs.Task;
        }

        [Test]
        public async Task SendRandomBytes_100_000 ()
        {
            await SendRandomBytes (100_000);
        }

        [Test]
        public async Task SendRandomBytes ([Values (1, 1399, 1401, 3000, 60_000, 100_000, 1024_000)] int size)
        {
            await SetupPeerPair ();

            for (int i = 0; i < 10; i++) {
                var sendBuffer = new byte[size];
                var receiveBuffer = new byte[size];
                Random.Shared.NextBytes (sendBuffer);

                int received = 0;
                async Task DrainConnection ()
                {
                    while (received != size) {
                        received += await Outgoing.ReceiveAsync (receiveBuffer.AsMemory (received)).WithTimeout (5000);
                    }
                }

                try {
                    var drainer = DrainConnection ();
                    await Incoming.SendAsync (sendBuffer).WithTimeout (5000);
                    await drainer.WithTimeout ();
                } catch {
                    TestContext.WriteLine ($"SendRandomBytes failed at iteration {i + 1}, size {size}, received {received}/{size}");
                    TestContext.WriteLine ($"Incoming: {Incoming.DiagnosticSnapshot}");
                    TestContext.WriteLine ($"Outgoing: {Outgoing.DiagnosticSnapshot}");
                    throw;
                }

                Assert.IsTrue (sendBuffer.AsSpan ().SequenceEqual (receiveBuffer));
            }
        }

        [Test]
        public async Task SendRandomBytes_ToUcatListener ([Values (1, 1399, 1401, 3000, 60_000)] int size)
        {
            var port = GetFreeUdpPort ();
            var server = StartUcatListener (port);
            var listener = new UtpPeerConnectionListener (
                new IPEndPoint (IPAddress.Loopback, 0),
                new UtpTransportSettings { InitialSynRetransmitTimeout = TimeSpan.FromMilliseconds (100) });
            UtpPeerConnection connection = null;

            try {
                listener.Start ();

                connection = new UtpPeerConnection (
                    listener,
                    listener.SendQueue,
                    new IPEndPoint (IPAddress.Loopback, port),
                    (ushort) RandomNumberGenerator.GetInt32 (1, ushort.MaxValue));

                try {
                    Assert.IsTrue (await connection.ConnectAsync ().WithTimeout (5000), "Could not connect to ucat.exe.");
                } catch {
                    Assert.Fail ($"Could not connect to ucat.exe. Log: {server.OutputPath}. Output: {server.ErrorOutput}");
                }

                var sendBuffer = new byte[size];
                var receiveBuffer = new byte[size];
                Random.Shared.NextBytes (sendBuffer);

                try {
                    var receiveTask = ReceiveExactlyAsync (connection, receiveBuffer).WithTimeout (10_000);
                    Assert.AreEqual (size, await connection.SendAsync (sendBuffer).WithTimeout (10_000), "Did not send the full payload.");
                    await receiveTask;
                } catch (Exception ex) {
                    Assert.Fail ($"ucat.exe did not echo the full payload. Process exited: {server.Process.HasExited}. Log: {server.OutputPath}. {ex}. Output: {server.ErrorOutput}");
                }

                Assert.IsTrue (sendBuffer.AsSpan ().SequenceEqual (receiveBuffer));
            } finally {
                connection?.Dispose ();
                listener.Stop ();
                StopUcatListener (server.Process);
            }
        }

        [TearDown]
        public void Teardown ()
        {
            Incoming?.Dispose ();
            IncomingListener?.Stop ();
            Outgoing?.Dispose ();
            OutgoingListener?.Stop ();
        }

        static int GetFreeUdpPort ()
        {
            using var socket = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind (new IPEndPoint (IPAddress.Loopback, 0));
            return ((IPEndPoint) socket.LocalEndPoint).Port;
        }

        sealed class UcatProcess
        {
            public Process Process { get; set; }
            public StringBuilder ErrorOutput { get; } = new StringBuilder ();
            public string OutputPath { get; set; }
        }

        static UcatProcess StartUcatListener (int port)
        {
            var ucat = FindUcatExecutable ();
            var outputPath = Path.Combine (TestContext.CurrentContext.WorkDirectory, $"ucat-{TestContext.CurrentContext.Test.ID}.log");
            File.Delete (outputPath);
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = ucat,
                    Arguments = $"-e -l -p {port}",
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName (ucat),
                }
            };
            var result = new UcatProcess { Process = process, OutputPath = outputPath };
            process.ErrorDataReceived += (o, e) => {
                if (e.Data != null) {
                    result.ErrorOutput.AppendLine (e.Data);
                    AppendUcatOutput (outputPath, "stderr", e.Data);
                }
            };
            process.OutputDataReceived += (o, e) => {
                if (e.Data != null)
                    AppendUcatOutput (outputPath, "stdout", e.Data);
            };

            try {
                Assert.IsTrue (process.Start (), "Could not start ucat.exe.");
                process.BeginErrorReadLine ();
                process.BeginOutputReadLine ();
                return result;
            } catch {
                process.Dispose ();
                throw;
            }
        }

        static void AppendUcatOutput (string path, string stream, string line)
        {
            lock (UcatOutputLocker)
                File.AppendAllText (path, $"[{stream}] {line}{Environment.NewLine}");
        }

        static void StopUcatListener (Process process)
        {
            try {
                if (!process.HasExited)
                    process.Kill (entireProcessTree: true);
            } finally {
                process.Dispose ();
            }
        }

        static async Task ReceiveExactlyAsync (UtpPeerConnection connection, Memory<byte> buffer)
        {
            int received = 0;
            while (received != buffer.Length) {
                var bytesRead = await connection.ReceiveAsync (buffer.Slice (received));
                if (bytesRead == 0)
                    throw new EndOfStreamException ("ucat.exe closed the connection before echoing the full payload.");

                received += bytesRead;
            }
        }

        static string FindUcatExecutable ()
        {
            var result = FindUcatExecutable (TestContext.CurrentContext.TestDirectory);
            if (result == null)
                result = FindUcatExecutable (Environment.CurrentDirectory);

            if (result == null)
                Assert.Inconclusive ("Could not find ucat.exe. The executable must be available in the repository root.");

            return result;
        }

        static string FindUcatExecutable (string startDirectory)
        {
            var directory = new DirectoryInfo (startDirectory);
            while (directory != null) {
                var candidate = Path.Combine (directory.FullName, "ucat.exe");
                if (File.Exists (candidate))
                    return candidate;

                candidate = Path.Combine (directory.FullName, "libutp", "Build", "x64", "Debug", "ucat.exe");
                if (File.Exists (candidate))
                    return candidate;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
