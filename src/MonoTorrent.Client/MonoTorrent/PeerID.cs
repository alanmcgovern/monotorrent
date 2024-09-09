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
        internal static Software Synthetic => new Software (new BEncodedString ("---- synthetic -----"));

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

            PeerId = peerId;
            string idAsText = peerId.Text;
            if (idAsText.StartsWith ("-WebSeed-", System.StringComparison.Ordinal)) {
                ShortId = "WebSeed";
                Client = ClientApp.WebSeed;
                return;
            }

            #region Standard style peers
            if ((m = standard.Match (idAsText)).Success) {
                ShortId = m.Groups[1].Value;
                switch (m.Groups[2].Value) {
                    case ("AG"):
                    case ("A~"):
                        Client = ClientApp.Ares;
                        break;
                    case ("AR"):
                        Client = ClientApp.Artic;
                        break;
                    case ("AT"):
                        Client = ClientApp.Artemis;
                        break;
                    case ("AX"):
                        Client = ClientApp.BitPump;
                        break;
                    case ("AV"):
                        Client = ClientApp.Avicora;
                        break;
                    case ("AZ"):
                        Client = ClientApp.Azureus;
                        break;
                    case ("BB"):
                        Client = ClientApp.BitBuddy;
                        break;

                    case ("BC"):
                        Client = ClientApp.BitComet;
                        break;

                    case ("BF"):
                        Client = ClientApp.Bitflu;
                        break;

                    case ("BS"):
                        Client = ClientApp.BTSlave;
                        break;

                    case ("BX"):
                        Client = ClientApp.BitTorrentX;
                        break;

                    case ("CD"):
                        Client = ClientApp.EnhancedCTorrent;
                        break;

                    case ("CT"):
                        Client = ClientApp.CTorrent;
                        break;

                    case ("DE"):
                        Client = ClientApp.DelugeTorrent;
                        break;

                    case ("EB"):
                        Client = ClientApp.EBit;
                        break;

                    case ("ES"):
                        Client = ClientApp.ElectricSheep;
                        break;

                    case ("KT"):
                        Client = ClientApp.KTorrent;
                        break;

                    case ("LP"):
                        Client = ClientApp.Lphant;
                        break;

                    case ("lt"):
                    case ("LT"):
                        Client = ClientApp.LibTorrent;
                        break;

                    case ("MP"):
                        Client = ClientApp.MooPolice;
                        break;

                    case ("MO"):
                        Client = ClientApp.MonoTorrent;
                        break;

                    case ("MT"):
                        Client = ClientApp.MoonlightTorrent;
                        break;

                    case ("qB"):
                        Client = ClientApp.qBittorrent;
                        break;

                    case ("QT"):
                        Client = ClientApp.Qt4Torrent;
                        break;

                    case ("RT"):
                        Client = ClientApp.Retriever;
                        break;

                    case ("SB"):
                        Client = ClientApp.Swiftbit;
                        break;

                    case ("SS"):
                        Client = ClientApp.SwarmScope;
                        break;

                    case ("SZ"):
                        Client = ClientApp.Shareaza;
                        break;

                    case ("TN"):
                        Client = ClientApp.TorrentDotNET;
                        break;

                    case ("TR"):
                        Client = ClientApp.Transmission;
                        break;

                    case ("TS"):
                        Client = ClientApp.Torrentstorm;
                        break;

                    case ("UL"):
                        Client = ClientApp.uLeecher;
                        break;

                    case ("UT"):
                        Client = ClientApp.uTorrent;
                        break;

                    case "UW":
                        Client = ClientApp.uTorrentWeb;
                        break;

                    case ("XT"):
                        Client = ClientApp.XanTorrent;
                        break;

                    case ("ZT"):
                        Client = ClientApp.ZipTorrent;
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
                ShortId = m.Groups[1].Value;
                Client = m.Groups[2].Value switch {
                    ("A") => ClientApp.ABC,
                    ("O") => ClientApp.OspreyPermaseed,
                    ("R") => ClientApp.Tribler,
                    ("S") => ClientApp.ShadowsClient,
                    ("T") => ClientApp.BitTornado,
                    ("U") => ClientApp.UPnPNatBitTorrent,
                    _ => ClientApp.Unknown,
                };
                return;
            }

            #endregion

            #region Brams Client
            if ((m = brahms.Match (idAsText)).Success) {
                ShortId = "M";
                Client = ClientApp.BitTorrent;
                return;
            }
            #endregion

            #region BitLord
            if ((m = bitlord.Match (idAsText)).Success) {
                Client = ClientApp.BitLord;
                ShortId = "lord";
                return;
            }
            #endregion

            #region BitComet
            if ((m = bitcomet.Match (idAsText)).Success) {
                Client = ClientApp.BitComet;
                ShortId = "BC";
                return;
            }
            #endregion

            #region XBT
            if ((m = xbt.Match (idAsText)).Success) {
                Client = ClientApp.XBTClient;
                ShortId = "XBT";
                return;
            }
            #endregion

            #region Opera
            if ((m = opera.Match (idAsText)).Success) {
                Client = ClientApp.Opera;
                ShortId = "OP";
            }
            #endregion

            #region MLDonkey
            if ((m = mldonkey.Match (idAsText)).Success) {
                Client = ClientApp.MLDonkey;
                ShortId = "ML";
                return;
            }
            #endregion

            #region Bits on wheels
            if ((m = bow.Match (idAsText)).Success) {
                Client = ClientApp.BitsOnWheels;
                ShortId = "BOW";
                return;
            }
            #endregion

            #region Queen Bee
            if ((m = queenbee.Match (idAsText)).Success) {
                Client = ClientApp.QueenBee;
                ShortId = "Q";
                return;
            }
            #endregion

            #region BitTornado special style
            if ((m = bittornado.Match (idAsText)).Success) {
                ShortId = m.Groups[1].Value;
                Client = ClientApp.BitTornado;
                return;
            }
            #endregion

            Client = ClientApp.Unknown;
            ShortId = idAsText;
        }


        public override string ToString ()
        {
            return ShortId;
        }
    }
}
