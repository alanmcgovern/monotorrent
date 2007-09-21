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
    public enum Client
    {
        ABC,
        Artic,
        BitPump,
        Azureus,
        BitBuddy,
        BitComet,
        Bitflu,
        BitLord,
        BitsOnWheels,
        BitTornado,
        BitTorrent,
        BTSlave,
        BittorrentX,
        EnhancedCTorrent,
        CTorrent,
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
        XanTorrent,
        XBTClient,
        ZipTorrent
    }

    public struct Software
    {
        private Client client;
        private string peerId;
        private string shortId;

        /// <summary>
        /// The name of the torrent software being used
        /// </summary>
        public Client Client
        {
            get { return this.client; }
        }

        /// <summary>
        /// The peer's ID
        /// </summary>
        internal string PeerId
        {
            get { return this.peerId; }
        }

        /// <summary>
        /// A shortened version of the peers ID
        /// </summary>
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
            r = new Regex(@"-(([A-Za-z]{2})\d{4})-*");
            if (r.IsMatch(peerId))
            {
                m = r.Match(peerId);

                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case ("AR"):
                        this.client = Common.Client.Artic;
                        break;

                    case ("AX"):
                        this.client = Common.Client.BitPump;
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
                        this.client = Common.Client.BittorrentX;
                        break;

                    case ("CD"):
                        this.client = Common.Client.EnhancedCTorrent;
                        break;

                    case ("CT"):
                        this.client = Common.Client.CTorrent;
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
