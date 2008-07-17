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
        AllPeers,
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
        BitSpirit,
        BitTornado,
        BitTorrent,
        BitTorrentX,
        BitTyrant,
        BramsClient,
        BTG,
        BTNextEvolution,
        BTQueue,
        BTSlave,
        CTorrent,
        DelugeTorrent,
        EBit,
        ElectricSheep,
        EnhancedCTorrent,
        FileCroc,
        FireTorrent,
        FlashGet,
        FoxTorrent,
        G3Torrent,
        GSTorrent,
        Halit,
        Hydranode,
        KGet,
        KTorrent,
        LHABC,
        LibTorrent,
        LimeWire,
        Lphant,
        Miro,
        MLDonkey,
        MonoTorrent,
        MoonlightTorrent,
        MooPolice,
        NetTransport,
        OmegaTorrent,
        Opera,
        OspreyPermaseed,
        Pando,
        PropagateDataClient,
        qBittorrent,
        QQDownload,
        Qt4Torrent,
        QueenBee,
        Retriever,
        Rufus,
        ShadowsClient,
        Shareaza,
        SharkTorrent,
        SwarmScope,
        Swiftbit,
        SymTorrent,
        TorrentDotNET,
        Torrentstorm,
        TorrentTopia,
        Transmission,
        Tribler,
        TuoTu,
        uLeecher,
        Unknown,
        UPnPNatBitTorrent,
        uTorrent,
        Vagaa,
        Vuze,
        XanTorrent,
        XBTClient,
        XTorrent,
        Xunlei,
        ZipTorrent
    }

    /// <summary>
    /// Class representing the various and sundry BitTorrent Clients lurking about on the web
    /// </summary>
    public struct Software
    {
        private Client client;
        private string peerId;
        private string shortId;

        public Client Client
        {
            get { return this.client; }
        }

        internal string PeerId
        {
            get { return this.peerId; }
        }

        public string ShortId
        {
            get { return this.shortId; }
        }


        internal Software(string peerId)
        {
            Match m;
            Regex r;

            this.peerId = peerId;

            #region Standard style peers
            r = new Regex(@"-(([A-Za-z\~]{2})\d{4})-*");
            if (r.IsMatch(peerId))
            {
                m = r.Match(peerId);

                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case ("AG"):
                    case ("A~"):
                        this.client = Common.Client.Ares;
                        break;
                    case ("AR"):
                        this.client = Common.Client.Artic;
                        break;
                    case ("AT"):
                        this.client = Common.Client.Artemis;
                        break;
                    case ("AX"):
                        this.client = Common.Client.BitPump;
                        break;
                    case ("AV"):
                        this.client = Common.Client.Avicora;
                        break;
                    case ("AZ"):
                        this.client = Common.Client.Azureus;
                        break;
                    case ("BB"):
                        this.client = Common.Client.BitBuddy;
                        break;

                    case ("BC"):
                        this.client = Common.Client.BitComet;
                        break;

                    case ("BF"):
                        this.client = Common.Client.Bitflu;
                        break;

                    case ("BS"):
                        this.client = Common.Client.BTSlave;
                        break;

                    case ("BX"):
                        this.client = Common.Client.BitTorrentX;
                        break;

                    case ("CD"):
                        this.client = Common.Client.EnhancedCTorrent;
                        break;

                    case ("CT"):
                        this.client = Common.Client.CTorrent;
                        break;

                    case ("DE"):
                        this.client = Common.Client.DelugeTorrent;
                        break;

                    case ("EB"):
                        this.client = Common.Client.EBit;
                        break;

                    case ("ES"):
                        this.client = Common.Client.ElectricSheep;
                        break;

                    case ("KT"):
                        this.client = Common.Client.KTorrent;
                        break;

                    case ("LP"):
                        this.client = Common.Client.Lphant;
                        break;

                    case ("lt"):
                    case ("LT"):
                        this.client = Common.Client.LibTorrent;
                        break;

                    case ("MP"):
                        this.client = Common.Client.MooPolice;
                        break;

                    case ("MO"):
                        this.client = Common.Client.MonoTorrent;
                        break;

                    case ("MT"):
                        this.client = Common.Client.MoonlightTorrent;
                        break;

                    case ("qB"):
                        this.client = Common.Client.qBittorrent;
                        break;

                    case ("QT"):
                        this.client = Common.Client.Qt4Torrent;
                        break;

                    case ("RT"):
                        this.client = Common.Client.Retriever;
                        break;

                    case ("SB"):
                        this.client = Common.Client.Swiftbit;
                        break;

                    case ("SS"):
                        this.client = Common.Client.SwarmScope;
                        break;

                    case ("SZ"):
                        this.client = Common.Client.Shareaza;
                        break;

                    case ("TN"):
                        this.client = Common.Client.TorrentDotNET;
                        break;

                    case ("TR"):
                        this.client = Common.Client.Transmission;
                        break;

                    case ("TS"):
                        this.client = Common.Client.Torrentstorm;
                        break;

                    case ("UL"):
                        this.client = Common.Client.uLeecher;
                        break;

                    case ("UT"):
                        this.client = Common.Client.uTorrent;
                        break;

                    case ("XT"):
                        this.client = Common.Client.XanTorrent;
                        break;

                    case ("ZT"):
                        this.client = Common.Client.ZipTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine("Unsupported standard style: " + m.Groups[2].Value);
                        this.client = Client.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Shadows Style
            r = new Regex(@"(([A-Za-z]{1})\d{3})----*");
            if (r.IsMatch(peerId))
            {
                m = r.Match(peerId);
                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case ("A"):
                        this.client = Client.ABC;
                        break;

                    case ("O"):
                        this.client = Client.OspreyPermaseed;
                        break;

                    case ("R"):
                        this.client = Client.Tribler;
                        break;

                    case ("S"):
                        this.client = Client.ShadowsClient;
                        break;

                    case ("T"):
                        this.client = Client.BitTornado;
                        break;

                    case ("U"):
                        this.client = Client.UPnPNatBitTorrent;
                        break;

                    default:
                        System.Diagnostics.Trace.WriteLine("Unsupported shadows style: " + m.Groups[2].Value);
                        this.client = Client.Unknown;
                        break;
                }
                return;
            }
            #endregion

            #region Brams Client
            r = new Regex("M/d-/d-/d--");
            if (r.IsMatch(peerId))
            {
                this.shortId = "M";
                this.client = Client.BitTorrent;
                return;
            }
            #endregion

            #region BitLord
            r = new Regex("exbc..LORD");
            if (r.IsMatch(peerId))
            {
                this.client = Client.BitLord;
                this.shortId = "lord";
                return;
            }
            #endregion

            #region BitComet
            r = new Regex("exbc");
            if (r.IsMatch(peerId))
            {
                this.client = Client.BitComet;
                this.shortId = "BC";
                return;
            }
            #endregion

            #region XBT
            r = new Regex("XBT/d/{3}");
            if (r.IsMatch(peerId))
            {
                this.client = Client.XBTClient;
                this.shortId = "XBT";
                return;
            }
            #endregion

            #region Opera
            r = new Regex("OP/d{4}");
            if (r.IsMatch(peerId))
            {
                this.client = Client.Opera;
                this.shortId = "OP";
            }
            #endregion

            #region MLDonkey
            r = new Regex("-ML/d\\./d\\./d");
            if (r.IsMatch(peerId))
            {
                this.client = Client.MLDonkey;
                this.shortId = "ML";
                return;
            }
            #endregion

            #region Bits on wheels
            r = new Regex("-BOWA");
            if (r.IsMatch(peerId))
            {
                this.client = Client.BitsOnWheels;
                this.shortId = "BOW";
                return;
            }
            #endregion

            #region Queen Bee
            r = new Regex("Q/d-/d-/d--");
            if (r.IsMatch(peerId))
            {
                this.client = Client.QueenBee;
                this.shortId = "Q";
                return;
            }
            #endregion

            #region BitTornado special style
            r = new Regex(@"(([A-Za-z]{1})\d{2}[A-Za-z]{1})----*");
            if(r.IsMatch(peerId))
            {
                m = r.Match(peerId);
                this.shortId = m.Groups[1].Value;
                this.client = Client.BitTornado;
                return;
            }
            #endregion

            this.client = Client.Unknown;
            this.shortId = peerId;
            System.Diagnostics.Trace.WriteLine("Unrecognisable clientid style: " + peerId);
        }


        public override string ToString()
        {
            return this.shortId;
        }
    }
}
