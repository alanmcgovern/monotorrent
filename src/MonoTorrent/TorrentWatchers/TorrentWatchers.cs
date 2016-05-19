using MonoTorrent.Common;

namespace MonoTorrent.TorrentWatcher
{
    /// <summary>
    ///     Main controller class for ITorrentWatcher
    /// </summary>
    public class TorrentWatchers : MonoTorrentCollection<ITorrentWatcher>
    {
        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        public void StartAll()
        {
            for (var i = 0; i < Count; i++)
                this[i].Start();
        }


        /// <summary>
        /// </summary>
        public void StopAll()
        {
            for (var i = 0; i < Count; i++)
                this[i].Stop();
        }


        /// <summary>
        /// </summary>
        public void ForceScanAll()
        {
            for (var i = 0; i < Count; i++)
                this[i].ForceScan();
        }

        #endregion
    }
}