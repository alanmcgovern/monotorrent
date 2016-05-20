namespace MonoTorrent.Client
{
    internal interface IUnchoker
    {
        void Choke(PeerId id);
        void UnchokeReview();
        void Unchoke(PeerId id);
    }
}