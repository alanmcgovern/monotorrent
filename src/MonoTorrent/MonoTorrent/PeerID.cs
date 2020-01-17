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
        private ClientApp client;
        private BEncodedString peerId;
        private string shortId;

        /// <summary>
        /// The name of the torrent software being used
        /// </summary>
        /// <value>The client.</value>
        public ClientApp Client {
            get { return this.client; }
        }

        /// <summary>
        /// The peer's ID
        /// </summary>
        /// <value>The peer id.</value>
        internal BEncodedString PeerId {
            get { return this.peerId; }
        }

        /// <summary>
        /// A shortened version of the peers ID
        /// </summary>
        /// <value>The short id.</value>
        public string ShortId {
            get { return this.shortId; }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Software"/> class.
        /// </summary>
        /// <param name="peerId">The peer id.</param>
        internal Software (BEncodedString peerId)
        {
            Match m;

            this.peerId = peerId;
            var idAsText = peerId.Text;
            if (idAsText.StartsWith ("-WebSeed-", System.StringComparison.Ordinal)) {
                this.shortId = "WebSeed";
                this.client = ClientApp.WebSeed;
                return;
            }

            #region Standard style peers
            if ((m = standard.Match (idAsText)).Success) {
                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value) {
                    case ("AG"):
                    case ("A~"):
                        this.client = ClientApp.Ares;
                        break;
                    case ("AR"):
                        this.client = ClientApp.Artic;
                        break;
                    case ("AT"):
                        this.client = ClientApp.Artemis;
                        break;
                    case ("AX"):
                        this.client = ClientApp.BitPump;
                        break;
                    case ("AV"):
                        this.client = ClientApp.Avicora;
                        break;
                    case ("AZ"):
                        this.client = ClientApp.Azureus;
                        break;
                    case ("BB"):
                        this.client = ClientApp.BitBuddy;
                        break;

                    case ("BC"):
                        this.client = ClientApp.BitComet;
                        break;

                    case ("BF"):
                        this.client = ClientApp.Bitflu;
                        break;

                    case ("BS"):
                        this.client = ClientApp.BTSlave;
                        break;

                    case ("BX"):
                        this.client = ClientApp.BitTorrentX;
                        break;

                    case ("CD"):
                        this.client = ClientApp.EnhancedCTorrent;
                        break;

                    case ("CT"):
                        this.client = ClientApp.CTorrent;
                        break;

                    case ("DE"):
                        this.client = ClientApp.DelugeTorrent;
                        break;

                    case ("EB"):
                        this.client = ClientApp.EBit;
                        break;

                    case ("ES"):
                        this.client = ClientApp.ElectricSheep;
                        break;

                    case ("KT"):
                        this.client = ClientApp.KTorrent;
                        break;

                    case ("LP"):
                        this.client = ClientApp.Lphant;
                        break;

                    case ("lt"):
                    case ("LT"):
                        this.client = ClientApp.LibTorrent;
                        break;

                    case ("MP"):
                        this.client = ClientApp.MooPolice;
                        break;

                    case ("MO"):
                        this.client = ClientApp.MonoTorrent;
                        break;

                    case ("MT"):
                        this.client = ClientApp.MoonlightTorrent;
                        break;

                    case ("qB"):
                        this.client = ClientApp.qBittorrent;
                        break;

                    case ("QT"):
                        this.client = ClientApp.Qt4Torrent;
                        break;

                    case ("RT"):
                        this.client = ClientApp.Retriever;
                        break;

                    case ("SB"):
                        this.client = ClientApp.Swiftbit;
                        break;

                    case ("SS"):
                        this.client = ClientApp.SwarmScope;
                        break;

                    case ("SZ"):
                        this.client = ClientApp.Shareaza;
                        break;

                    case ("TN"):
                        this.client = ClientApp.TorrentDotNET;
                        break;

                    case ("TR"):
                        this.client = ClientApp.Transmission;
                        break;

                    case ("TS"):
                        this.client = ClientApp.Torrentstorm;
                        break;

                    case ("UL"):
                        this.client = ClientApp.uLeecher;
                        break;

                    case ("UT"):
                        this.client = ClientApp.uTorrent;
                        break;

                    case ("XT"):
                        this.client = ClientApp.XanTorrent;
                        break;

                    case ("ZT"):
                        this.client = ClientApp.ZipTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine ("Unsupported standard style: " + m.Groups[2].Value);
                        client = ClientApp.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Shadows Style
            if ((m = shadows.Match (idAsText)).Success) {
                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value) {
                    case ("A"):
                        this.client = ClientApp.ABC;
                        break;

                    case ("O"):
                        this.client = ClientApp.OspreyPermaseed;
                        break;

                    case ("R"):
                        this.client = ClientApp.Tribler;
                        break;

                    case ("S"):
                        this.client = ClientApp.ShadowsClient;
                        break;

                    case ("T"):
                        this.client = ClientApp.BitTornado;
                        break;

                    case ("U"):
                        this.client = ClientApp.UPnPNatBitTorrent;
                        break;

                    default:
                        this.client = ClientApp.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Brams Client
            if ((m = brahms.Match (idAsText)).Success) {
                this.shortId = "M";
                this.client = ClientApp.BitTorrent;
                return;
            }
            #endregion

            #region BitLord
            if ((m = bitlord.Match (idAsText)).Success) {
                this.client = ClientApp.BitLord;
                this.shortId = "lord";
                return;
            }
            #endregion

            #region BitComet
            if ((m = bitcomet.Match (idAsText)).Success) {
                this.client = ClientApp.BitComet;
                this.shortId = "BC";
                return;
            }
            #endregion

            #region XBT
            if ((m = xbt.Match (idAsText)).Success) {
                this.client = ClientApp.XBTClient;
                this.shortId = "XBT";
                return;
            }
            #endregion

            #region Opera
            if ((m = opera.Match (idAsText)).Success) {
                this.client = ClientApp.Opera;
                this.shortId = "OP";
            }
            #endregion

            #region MLDonkey
            if ((m = mldonkey.Match (idAsText)).Success) {
                this.client = ClientApp.MLDonkey;
                this.shortId = "ML";
                return;
            }
            #endregion

            #region Bits on wheels
            if ((m = bow.Match (idAsText)).Success) {
                this.client = ClientApp.BitsOnWheels;
                this.shortId = "BOW";
                return;
            }
            #endregion

            #region Queen Bee
            if ((m = queenbee.Match (idAsText)).Success) {
                this.client = ClientApp.QueenBee;
                this.shortId = "Q";
                return;
            }
            #endregion

            #region BitTornado special style
            if ((m = bittornado.Match (idAsText)).Success) {
                this.shortId = m.Groups[1].Value;
                this.client = ClientApp.BitTornado;
                return;
            }
            #endregion

            this.client = ClientApp.Unknown;
            this.shortId = idAsText;
        }


        public override string ToString ()
        {
            return this.shortId;
        }
    }
}
