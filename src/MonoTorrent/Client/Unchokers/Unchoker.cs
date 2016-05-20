using System;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    internal abstract class Unchoker : IUnchoker
    {
        public virtual void Choke(PeerId id)
        {
            id.AmChoking = true;
            id.TorrentManager.UploadingTo--;
            id.Enqueue(new ChokeMessage());
        }

        public abstract void UnchokeReview();

        public virtual void Unchoke(PeerId id)
        {
            id.AmChoking = false;
            id.TorrentManager.UploadingTo++;
            id.Enqueue(new UnchokeMessage());
            id.LastUnchoked = DateTime.Now;
        }
    }
}