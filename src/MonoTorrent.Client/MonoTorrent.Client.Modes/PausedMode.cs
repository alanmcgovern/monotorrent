//
// PausedMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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


namespace MonoTorrent.Client.Modes
{
    class PausedMode : Mode
    {
        public override TorrentState State => TorrentState.Paused;

        public PausedMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings)
            : base (manager, diskManager, connectionManager, settings)
        {
            // When in the Paused mode, a special RateLimiter will
            // activate and disable transfers. PauseMode itself
            // does not need to do anything special.
        }

        public override void HandleFilePriorityChanged (ITorrentManagerFile file, Priority oldPriority)
            => RefreshAmInterestedStatusForAllPeers ();

        public override void Tick (int counter)
        {
            // TODO: In future maybe this can be made smarter by refactoring
            // so that in Pause mode we set the Interested status of all peers
            // to false, so no data is requested. This way connections can be
            // kept open by sending/receiving KeepAlive messages. Currently
            // we 'Pause' by not sending/receiving data from the socket.
        }
    }
}
