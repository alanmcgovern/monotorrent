//
// PeerID.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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


using System.Text.RegularExpressions;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    /// <summary>
    /// BitTorrrent 
    /// </summary>
    /// <remarks>
    /// Good place for information about BT peer ID conventions:
    ///     http://wiki.theory.org/BitTorrentSpecification
    ///     http://transmission.m0k.org/trac/browser/trunk/libtransmission/clients.c (hello Transmission authors!) :)
    ///     http://rufus.cvs.sourceforge.net/rufus/Rufus/g3peerid.py?view=log (for older clients)
    ///     http://shareaza.svn.sourceforge.net/viewvc/shareaza/trunk/shareaza/BTClient.cpp?view=markup
    ///     http://libtorrent.rakshasa.no/browser/trunk/libtorrent/src/torrent/peer/client_list.cc
    /// </remarks>
    public enum ClientApp
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
        uTorrentWeb,
        UPnPNatBitTorrent,
        Vuze,
        WebSeed,
        XanTorrent,
        XBTClient,
        ZipTorrent
    }

    /// <summary>
    /// Class representing the various and sundry BitTorrent Clients lurking about on the web
    /// </summary>
    public struct Software
    {
        static readonly Regex bow = new Regex ("-BOWA");
        static readonly Regex brahms = new Regex ("M/d-/d-/d--");
        static readonly Regex bitlord = new Regex ("exbc..LORD");
        static readonly Regex bittornado = new Regex (@"(([A-Za-z]{1})\d{2}[A-Za-z]{1})----*");
        static readonly Regex bitcomet = new Regex ("exbc");
        static readonly Regex mldonkey = new Regex ("-ML/d\\./d\\./d");
        static readonly Regex opera = new Regex ("OP/d{4}");
        static readonly Regex queenbee = new Regex ("Q/d-/d-/d--");
        static readonly Regex standard = new Regex (@"-(([A-Za-z\~]{2})\d{4})-*");
        static readonly Regex shadows = new Regex (@"(([A-Za-z]{1})\d{3})----*");
        static readonly Regex xbt = new Regex ("XBT/d/{3}");

        /// <summary>
        /// The name of the torrent software being used
        /// </summary>
        /// <value>The client.</value>
        public ClientApp Client { get; }

        /// <summary>
        /// The peer's ID
        /// </summary>
        /// <value>The peer id.</value>
        internal BEncodedString PeerId { get; }

        /// <summary>
        /// A shortened version of the peers ID
        /// </summary>
        /// <value>The short id.</value>
        public string ShortId { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Software"/> class.
        /// </summary>
        /// <param name="peerId">The peer id.</param>
        internal Software (BEncodedString peerId)
        {
            Match m;

            this.PeerId = peerId;
            var idAsText = peerId.Text;
            if (idAsText.StartsWith ("-WebSeed-", System.StringComparison.Ordinal)) {
                this.ShortId = "WebSeed";
                this.Client = ClientApp.WebSeed;
                return;
            }

            #region Standard style peers
            if ((m = standard.Match (idAsText)).Success) {
                this.ShortId = m.Groups[1].Value;
                switch (m.Groups[2].Value) {
                    case ("AG"):
                    case ("A~"):
                        this.Client = ClientApp.Ares;
                        break;
                    case ("AR"):
                        this.Client = ClientApp.Artic;
                        break;
                    case ("AT"):
                        this.Client = ClientApp.Artemis;
                        break;
                    case ("AX"):
                        this.Client = ClientApp.BitPump;
                        break;
                    case ("AV"):
                        this.Client = ClientApp.Avicora;
                        break;
                    case ("AZ"):
                        this.Client = ClientApp.Azureus;
                        break;
                    case ("BB"):
                        this.Client = ClientApp.BitBuddy;
                        break;

                    case ("BC"):
                        this.Client = ClientApp.BitComet;
                        break;

                    case ("BF"):
                        this.Client = ClientApp.Bitflu;
                        break;

                    case ("BS"):
                        this.Client = ClientApp.BTSlave;
                        break;

                    case ("BX"):
                        this.Client = ClientApp.BitTorrentX;
                        break;

                    case ("CD"):
                        this.Client = ClientApp.EnhancedCTorrent;
                        break;

                    case ("CT"):
                        this.Client = ClientApp.CTorrent;
                        break;

                    case ("DE"):
                        this.Client = ClientApp.DelugeTorrent;
                        break;

                    case ("EB"):
                        this.Client = ClientApp.EBit;
                        break;

                    case ("ES"):
                        this.Client = ClientApp.ElectricSheep;
                        break;

                    case ("KT"):
                        this.Client = ClientApp.KTorrent;
                        break;

                    case ("LP"):
                        this.Client = ClientApp.Lphant;
                        break;

                    case ("lt"):
                    case ("LT"):
                        this.Client = ClientApp.LibTorrent;
                        break;

                    case ("MP"):
                        this.Client = ClientApp.MooPolice;
                        break;

                    case ("MO"):
                        this.Client = ClientApp.MonoTorrent;
                        break;

                    case ("MT"):
                        this.Client = ClientApp.MoonlightTorrent;
                        break;

                    case ("qB"):
                        this.Client = ClientApp.qBittorrent;
                        break;

                    case ("QT"):
                        this.Client = ClientApp.Qt4Torrent;
                        break;

                    case ("RT"):
                        this.Client = ClientApp.Retriever;
                        break;

                    case ("SB"):
                        this.Client = ClientApp.Swiftbit;
                        break;

                    case ("SS"):
                        this.Client = ClientApp.SwarmScope;
                        break;

                    case ("SZ"):
                        this.Client = ClientApp.Shareaza;
                        break;

                    case ("TN"):
                        this.Client = ClientApp.TorrentDotNET;
                        break;

                    case ("TR"):
                        this.Client = ClientApp.Transmission;
                        break;

                    case ("TS"):
                        this.Client = ClientApp.Torrentstorm;
                        break;

                    case ("UL"):
                        this.Client = ClientApp.uLeecher;
                        break;

                    case ("UT"):
                        this.Client = ClientApp.uTorrent;
                        break;

                    case "UW":
                        this.Client = ClientApp.uTorrentWeb;
                        break;

                    case ("XT"):
                        this.Client = ClientApp.XanTorrent;
                        break;

                    case ("ZT"):
                        this.Client = ClientApp.ZipTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine ($"Unsupported standard style: {m.Groups[2].Value}");
                        Client = ClientApp.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Shadows Style
            if ((m = shadows.Match (idAsText)).Success) {
                this.ShortId = m.Groups[1].Value;
                switch (m.Groups[2].Value) {
                    case ("A"):
                        this.Client = ClientApp.ABC;
                        break;

                    case ("O"):
                        this.Client = ClientApp.OspreyPermaseed;
                        break;

                    case ("R"):
                        this.Client = ClientApp.Tribler;
                        break;

                    case ("S"):
                        this.Client = ClientApp.ShadowsClient;
                        break;

                    case ("T"):
                        this.Client = ClientApp.BitTornado;
                        break;

                    case ("U"):
                        this.Client = ClientApp.UPnPNatBitTorrent;
                        break;

                    default:
                        this.Client = ClientApp.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Brams Client
            if ((m = brahms.Match (idAsText)).Success) {
                this.ShortId = "M";
                this.Client = ClientApp.BitTorrent;
                return;
            }
            #endregion

            #region BitLord
            if ((m = bitlord.Match (idAsText)).Success) {
                this.Client = ClientApp.BitLord;
                this.ShortId = "lord";
                return;
            }
            #endregion

            #region BitComet
            if ((m = bitcomet.Match (idAsText)).Success) {
                this.Client = ClientApp.BitComet;
                this.ShortId = "BC";
                return;
            }
            #endregion

            #region XBT
            if ((m = xbt.Match (idAsText)).Success) {
                this.Client = ClientApp.XBTClient;
                this.ShortId = "XBT";
                return;
            }
            #endregion

            #region Opera
            if ((m = opera.Match (idAsText)).Success) {
                this.Client = ClientApp.Opera;
                this.ShortId = "OP";
            }
            #endregion

            #region MLDonkey
            if ((m = mldonkey.Match (idAsText)).Success) {
                this.Client = ClientApp.MLDonkey;
                this.ShortId = "ML";
                return;
            }
            #endregion

            #region Bits on wheels
            if ((m = bow.Match (idAsText)).Success) {
                this.Client = ClientApp.BitsOnWheels;
                this.ShortId = "BOW";
                return;
            }
            #endregion

            #region Queen Bee
            if ((m = queenbee.Match (idAsText)).Success) {
                this.Client = ClientApp.QueenBee;
                this.ShortId = "Q";
                return;
            }
            #endregion

            #region BitTornado special style
            if ((m = bittornado.Match (idAsText)).Success) {
                this.ShortId = m.Groups[1].Value;
                this.Client = ClientApp.BitTornado;
                return;
            }
            #endregion

            this.Client = ClientApp.Unknown;
            this.ShortId = idAsText;
        }


        public override string ToString ()
        {
            return this.ShortId;
        }
    }
}
