using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MonoTorrent.Common
{
    /// <summary>
    ///     BitTorrrent
    /// </summary>
    /// <remarks>
    ///     Good place for information about BT peer ID conventions:
    ///     http://wiki.theory.org/BitTorrentSpecification
    ///     http://transmission.m0k.org/trac/browser/trunk/libtransmission/clients.c (hello Transmission authors!) :)
    ///     http://rufus.cvs.sourceforge.net/rufus/Rufus/g3peerid.py?view=log (for older clients)
    ///     http://shareaza.svn.sourceforge.net/viewvc/shareaza/trunk/shareaza/BTClient.cpp?view=markup
    ///     http://libtorrent.rakshasa.no/browser/trunk/libtorrent/src/torrent/peer/client_list.cc
    /// </remarks>
    public enum Client
    {
        ABC,
        Ares,
        Artemis,
        Artic,
        Avicora,
        Azureus,
        BitBuddy,
        BitComet,
        Bitflu,
        BitLet,
        BitLord,
        BitPump,
        BitRocket,
        BitsOnWheels,
        BTSlave,
        BitSpirit,
        BitTornado,
        BitTorrent,
        BitTorrentX,
        BTG,
        EnhancedCTorrent,
        CTorrent,
        DelugeTorrent,
        EBit,
        ElectricSheep,
        KTorrent,
        Lphant,
        LibTorrent,
        MLDonkey,
        MooPolice,
        MoonlightTorrent,
        MonoTorrent,
        Opera,
        OspreyPermaseed,
        qBittorrent,
        QueenBee,
        Qt4Torrent,
        Retriever,
        ShadowsClient,
        Swiftbit,
        SwarmScope,
        Shareaza,
        TorrentDotNET,
        Transmission,
        Tribler,
        Torrentstorm,
        uLeecher,
        Unknown,
        uTorrent,
        UPnPNatBitTorrent,
        Vuze,
        WebSeed,
        XanTorrent,
        XBTClient,
        ZipTorrent
    }

    /// <summary>
    ///     Class representing the various and sundry BitTorrent Clients lurking about on the web
    /// </summary>
    public struct Software
    {
        private static readonly Regex bow = new Regex("-BOWA");
        private static readonly Regex brahms = new Regex("M/d-/d-/d--");
        private static readonly Regex bitlord = new Regex("exbc..LORD");
        private static readonly Regex bittornado = new Regex(@"(([A-Za-z]{1})\d{2}[A-Za-z]{1})----*");
        private static readonly Regex bitcomet = new Regex("exbc");
        private static readonly Regex mldonkey = new Regex("-ML/d\\./d\\./d");
        private static readonly Regex opera = new Regex("OP/d{4}");
        private static readonly Regex queenbee = new Regex("Q/d-/d-/d--");
        private static readonly Regex standard = new Regex(@"-(([A-Za-z\~]{2})\d{4})-*");
        private static readonly Regex shadows = new Regex(@"(([A-Za-z]{1})\d{3})----*");
        private static readonly Regex xbt = new Regex("XBT/d/{3}");

        /// <summary>
        ///     The name of the torrent software being used
        /// </summary>
        /// <value>The client.</value>
        public Client Client { get; }

        /// <summary>
        ///     The peer's ID
        /// </summary>
        /// <value>The peer id.</value>
        internal string PeerId { get; }

        /// <summary>
        ///     A shortened version of the peers ID
        /// </summary>
        /// <value>The short id.</value>
        public string ShortId { get; }


        /// <summary>
        ///     Initializes a new instance of the <see cref="Software" /> class.
        /// </summary>
        /// <param name="peerId">The peer id.</param>
        internal Software(string peerId)
        {
            Match m;

            PeerId = peerId;
            if (peerId.StartsWith("-WebSeed-"))
            {
                ShortId = "WebSeed";
                Client = Client.WebSeed;
                return;
            }

            #region Standard style peers

            if ((m = standard.Match(peerId)) != null)
            {
                ShortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case "AG":
                    case "A~":
                        Client = Client.Ares;
                        break;
                    case "AR":
                        Client = Client.Artic;
                        break;
                    case "AT":
                        Client = Client.Artemis;
                        break;
                    case "AX":
                        Client = Client.BitPump;
                        break;
                    case "AV":
                        Client = Client.Avicora;
                        break;
                    case "AZ":
                        Client = Client.Azureus;
                        break;
                    case "BB":
                        Client = Client.BitBuddy;
                        break;

                    case "BC":
                        Client = Client.BitComet;
                        break;

                    case "BF":
                        Client = Client.Bitflu;
                        break;

                    case "BS":
                        Client = Client.BTSlave;
                        break;

                    case "BX":
                        Client = Client.BitTorrentX;
                        break;

                    case "CD":
                        Client = Client.EnhancedCTorrent;
                        break;

                    case "CT":
                        Client = Client.CTorrent;
                        break;

                    case "DE":
                        Client = Client.DelugeTorrent;
                        break;

                    case "EB":
                        Client = Client.EBit;
                        break;

                    case "ES":
                        Client = Client.ElectricSheep;
                        break;

                    case "KT":
                        Client = Client.KTorrent;
                        break;

                    case "LP":
                        Client = Client.Lphant;
                        break;

                    case "lt":
                    case "LT":
                        Client = Client.LibTorrent;
                        break;

                    case "MP":
                        Client = Client.MooPolice;
                        break;

                    case "MO":
                        Client = Client.MonoTorrent;
                        break;

                    case "MT":
                        Client = Client.MoonlightTorrent;
                        break;

                    case "qB":
                        Client = Client.qBittorrent;
                        break;

                    case "QT":
                        Client = Client.Qt4Torrent;
                        break;

                    case "RT":
                        Client = Client.Retriever;
                        break;

                    case "SB":
                        Client = Client.Swiftbit;
                        break;

                    case "SS":
                        Client = Client.SwarmScope;
                        break;

                    case "SZ":
                        Client = Client.Shareaza;
                        break;

                    case "TN":
                        Client = Client.TorrentDotNET;
                        break;

                    case "TR":
                        Client = Client.Transmission;
                        break;

                    case "TS":
                        Client = Client.Torrentstorm;
                        break;

                    case "UL":
                        Client = Client.uLeecher;
                        break;

                    case "UT":
                        Client = Client.uTorrent;
                        break;

                    case "XT":
                        Client = Client.XanTorrent;
                        break;

                    case "ZT":
                        Client = Client.ZipTorrent;
                        break;

                    default:
                        Trace.WriteLine("Unsupported standard style: " + m.Groups[2].Value);
                        Client = Client.Unknown;
                        break;
                }
                return;
            }

            #endregion

            #region Shadows Style

            if ((m = shadows.Match(peerId)) != null)
            {
                ShortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case "A":
                        Client = Client.ABC;
                        break;

                    case "O":
                        Client = Client.OspreyPermaseed;
                        break;

                    case "R":
                        Client = Client.Tribler;
                        break;

                    case "S":
                        Client = Client.ShadowsClient;
                        break;

                    case "T":
                        Client = Client.BitTornado;
                        break;

                    case "U":
                        Client = Client.UPnPNatBitTorrent;
                        break;

                    default:
                        Trace.WriteLine("Unsupported shadows style: " + m.Groups[2].Value);
                        Client = Client.Unknown;
                        break;
                }
                return;
            }

            #endregion

            #region Brams Client

            if ((m = brahms.Match(peerId)) != null)
            {
                ShortId = "M";
                Client = Client.BitTorrent;
                return;
            }

            #endregion

            #region BitLord

            if ((m = bitlord.Match(peerId)) != null)
            {
                Client = Client.BitLord;
                ShortId = "lord";
                return;
            }

            #endregion

            #region BitComet

            if ((m = bitcomet.Match(peerId)) != null)
            {
                Client = Client.BitComet;
                ShortId = "BC";
                return;
            }

            #endregion

            #region XBT

            if ((m = xbt.Match(peerId)) != null)
            {
                Client = Client.XBTClient;
                ShortId = "XBT";
                return;
            }

            #endregion

            #region Opera

            if ((m = opera.Match(peerId)) != null)
            {
                Client = Client.Opera;
                ShortId = "OP";
            }

            #endregion

            #region MLDonkey

            if ((m = mldonkey.Match(peerId)) != null)
            {
                Client = Client.MLDonkey;
                ShortId = "ML";
                return;
            }

            #endregion

            #region Bits on wheels

            if ((m = bow.Match(peerId)) != null)
            {
                Client = Client.BitsOnWheels;
                ShortId = "BOW";
                return;
            }

            #endregion

            #region Queen Bee

            if ((m = queenbee.Match(peerId)) != null)
            {
                Client = Client.QueenBee;
                ShortId = "Q";
                return;
            }

            #endregion

            #region BitTornado special style

            if ((m = bittornado.Match(peerId)) != null)
            {
                ShortId = m.Groups[1].Value;
                Client = Client.BitTornado;
                return;
            }

            #endregion

            Client = Client.Unknown;
            ShortId = peerId;
            Trace.WriteLine("Unrecognisable clientid style: " + peerId);
        }


        public override string ToString()
        {
            return ShortId;
        }
    }
}