using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    internal class PausedMode : Mode
    {
        public PausedMode(TorrentManager manager)
            : base(manager)
        {
            // When in the Paused mode, a special RateLimiter will
            // activate and disable transfers. PauseMode itself
            // does not need to do anything special.
        }

        public override TorrentState State
        {
            get { return TorrentState.Paused; }
        }

        public override void Tick(int counter)
        {
            // TODO: In future maybe this can be made smarter by refactoring
            // so that in Pause mode we set the Interested status of all peers
            // to false, so no data is requested. This way connections can be
            // kept open by sending/receiving KeepAlive messages. Currently
            // we 'Pause' by not sending/receiving data from the socket.
        }
    }
}