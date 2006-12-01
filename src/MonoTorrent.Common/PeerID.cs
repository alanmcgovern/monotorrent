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
        BitTornado,
        BTSlave,
        BittorrentX,
        EnhancedCTorrent,
        CTorrent,
        EBit,
        ElectricSheep,
        KTorrent,
        Lphant,
        libtorrent,
        LibTorrent,
        MooPolice,
        MoonlightTorrent,
        MonoTorrent,
        OspreyPermaseed,
        qBittorrent,
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
        ZipTorrent
    }

    public struct PeerID
    {
        public Client Client
        {
            get { return this.client; }
        }
        private Client client;

        public string ShortId
        {
            get { return this.shortId; }
        }
        private string shortId;

#warning I only wrote support for Standard peerid's and Shadows style peer id's because i'm lazy.
        public PeerID(string peerId)
        {
            Match m;
            Regex r;

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

                    case ("LT"):
                        this.client = Common.Client.libtorrent;
                        break;

                    case ("lt"):
                        this.client = Common.Client.LibTorrent;
                        break;

                    case ("MP"):
                        this.client = Common.Client.MooPolice;
                        break;

                    case ("MN"):
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
            if(r.IsMatch(peerId))
            {
                m = r.Match(peerId);
                this.shortId = m.Groups[1].Value;
                switch (m.Groups[2].Value)
                {
                    case("A"):
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

            this.client = Client.Unknown;
            this.shortId = peerId;
            System.Diagnostics.Trace.WriteLine("Unsupported clientid style: " + peerId);
#endregion
        }
    }
}
