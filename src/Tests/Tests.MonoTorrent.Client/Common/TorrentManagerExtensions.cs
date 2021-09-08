//
// TorrentManagerExtensions.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.Client
{
    public static class TorrentManagerExtensions
    {
        static readonly TimeSpan Timeout = System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours (1) : TimeSpan.FromSeconds (5);

        public static async Task WaitForState (this TorrentManager manager, TorrentState state)
        {
            await ClientEngine.MainLoop;

            if (manager.State == state)
                return;

            var tcs = new TaskCompletionSource<object> ();
            var cancellation = new CancellationTokenSource (Timeout);
            var token = cancellation.Token.Register (() => tcs.TrySetCanceled ());

            manager.TorrentStateChanged += (o, e) => {
                if (e.NewState == state) {
                    token.Dispose ();
                    tcs.TrySetResult (null);
                }
            };

            await tcs.Task;
        }
    }
}
