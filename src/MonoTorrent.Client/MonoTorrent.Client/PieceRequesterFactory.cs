using System;

using MonoTorrent.PiecePicking;

namespace MonoTorrent.Client
{
    public static class PieceRequesterFactory
    {
        static Func<ITorrentData, IPieceRequester> StandardCreator = torrentData => new StandardPieceRequester ();
        static Func<ITorrentData, IStreamingPieceRequester> StreamingCreator = torrentData => new StreamingPieceRequester ();

        public static void RegisterStandardPieceRequester<T> (Func<ITorrentData, IPieceRequester> creator)
            where T : IPieceRequester
            => StandardCreator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static void RegisterStreamingPieceRequester<T> (Func<ITorrentData, IStreamingPieceRequester> creator)
            where T : IStreamingPieceRequester
            => StreamingCreator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static IPieceRequester CreateStandardPieceRequester (ITorrentData torrentData)
            => StandardCreator (torrentData);

        public static IStreamingPieceRequester CreateStreamingPieceRequester (ITorrentData torrentData)
            => StreamingCreator (torrentData);
    }
}