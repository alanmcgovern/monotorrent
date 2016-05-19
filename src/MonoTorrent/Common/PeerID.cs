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


using System;
using System.Text.RegularExpressions;

namespace MonoTorrent.Common
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
    /// Class representing the various and sundry BitTorrent Clients lurking about on the web
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
        private Client client;
        private string peerId;
        private string shortId;

        /// <summary>
        /// The name of the torrent software being used
        /// </summary>
        /// <value>The client.</value>
        public Client Client
        {
            get { return client; }
        }

        /// <summary>
        /// The peer's ID
        /// </summary>
        /// <value>The peer id.</value>
        internal string PeerId
        {
            get { return peerId; }
        }

        /// <summary>
        /// A shortened version of the peers ID
        /// </summary>
        /// <value>The short id.</value>
        public string ShortId
        {
            get { return shortId; }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Software"/> class.
        /// </summary>
        /// <param name="peerId">The peer id.</param>
        internal Software(string peerId)
        {
            Match m;

            this.peerId = peerId;
            if (peerId.StartsWith("-WebSeed-"))
            {
                shortId = "WebSeed";
                client = Client.WebSeed;
                return;
            }

            #region Standard style peers

            if ((m = standard.Match(peerId)) != null)
            {
                shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case "AG":
                    case "A~":
                        client = Client.Ares;
                        break;
                    case "AR":
                        client = Client.Artic;
                        break;
                    case "AT":
                        client = Client.Artemis;
                        break;
                    case "AX":
                        client = Client.BitPump;
                        break;
                    case "AV":
                        client = Client.Avicora;
                        break;
                    case "AZ":
                        client = Client.Azureus;
                        break;
                    case "BB":
                        client = Client.BitBuddy;
                        break;

                    case "BC":
                        client = Client.BitComet;
                        break;

                    case "BF":
                        client = Client.Bitflu;
                        break;

                    case "BS":
                        client = Client.BTSlave;
                        break;

                    case "BX":
                        client = Client.BitTorrentX;
                        break;

                    case "CD":
                        client = Client.EnhancedCTorrent;
                        break;

                    case "CT":
                        client = Client.CTorrent;
                        break;

                    case "DE":
                        client = Client.DelugeTorrent;
                        break;

                    case "EB":
                        client = Client.EBit;
                        break;

                    case "ES":
                        client = Client.ElectricSheep;
                        break;

                    case "KT":
                        client = Client.KTorrent;
                        break;

                    case "LP":
                        client = Client.Lphant;
                        break;

                    case "lt":
                    case "LT":
                        client = Client.LibTorrent;
                        break;

                    case "MP":
                        client = Client.MooPolice;
                        break;

                    case "MO":
                        client = Client.MonoTorrent;
                        break;

                    case "MT":
                        client = Client.MoonlightTorrent;
                        break;

                    case "qB":
                        client = Client.qBittorrent;
                        break;

                    case "QT":
                        client = Client.Qt4Torrent;
                        break;

                    case "RT":
                        client = Client.Retriever;
                        break;

                    case "SB":
                        client = Client.Swiftbit;
                        break;

                    case "SS":
                        client = Client.SwarmScope;
                        break;

                    case "SZ":
                        client = Client.Shareaza;
                        break;

                    case "TN":
                        client = Client.TorrentDotNET;
                        break;

                    case "TR":
                        client = Client.Transmission;
                        break;

                    case "TS":
                        client = Client.Torrentstorm;
                        break;

                    case "UL":
                        client = Client.uLeecher;
                        break;

                    case "UT":
                        client = Client.uTorrent;
                        break;

                    case "XT":
                        client = Client.XanTorrent;
                        break;

                    case "ZT":
                        client = Client.ZipTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine("Unsupported standard style: " + m.Groups[2].Value);
                        client = Client.Unknown;
                        break;
                }
                return;
            }

            #endregion

            #region Shadows Style

            if ((m = shadows.Match(peerId)) != null)
            {
                shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case "A":
                        client = Client.ABC;
                        break;

                    case "O":
                        client = Client.OspreyPermaseed;
                        break;

                    case "R":
                        client = Client.Tribler;
                        break;

                    case "S":
                        client = Client.ShadowsClient;
                        break;

                    case "T":
                        client = Client.BitTornado;
                        break;

                    case "U":
                        client = Client.UPnPNatBitTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine("Unsupported shadows style: " + m.Groups[2].Value);
                        client = Client.Unknown;
                        break;
                }
                return;
            }

            #endregion

            #region Brams Client

            if ((m = brahms.Match(peerId)) != null)
            {
                shortId = "M";
                client = Client.BitTorrent;
                return;
            }

            #endregion

            #region BitLord

            if ((m = bitlord.Match(peerId)) != null)
            {
                client = Client.BitLord;
                shortId = "lord";
                return;
            }

            #endregion

            #region BitComet

            if ((m = bitcomet.Match(peerId)) != null)
            {
                client = Client.BitComet;
                shortId = "BC";
                return;
            }

            #endregion

            #region XBT

            if ((m = xbt.Match(peerId)) != null)
            {
                client = Client.XBTClient;
                shortId = "XBT";
                return;
            }

            #endregion

            #region Opera

            if ((m = opera.Match(peerId)) != null)
            {
                client = Client.Opera;
                shortId = "OP";
            }

            #endregion

            #region MLDonkey

            if ((m = mldonkey.Match(peerId)) != null)
            {
                client = Client.MLDonkey;
                shortId = "ML";
                return;
            }

            #endregion

            #region Bits on wheels

            if ((m = bow.Match(peerId)) != null)
            {
                client = Client.BitsOnWheels;
                shortId = "BOW";
                return;
            }

            #endregion

            #region Queen Bee

            if ((m = queenbee.Match(peerId)) != null)
            {
                client = Client.QueenBee;
                shortId = "Q";
                return;
            }

            #endregion

            #region BitTornado special style

            if ((m = bittornado.Match(peerId)) != null)
            {
                shortId = m.Groups[1].Value;
                client = Client.BitTornado;
                return;
            }

            #endregion

            client = Client.Unknown;
            shortId = peerId;
            System.Diagnostics.Trace.WriteLine("Unrecognisable clientid style: " + peerId);
        }


        public override string ToString()
        {
            return shortId;
        }
    }
}