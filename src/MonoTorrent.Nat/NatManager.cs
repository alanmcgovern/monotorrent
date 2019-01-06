#if !DISABLE_NAT
using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Timers;
using System.Linq;
using System.Diagnostics;
using MonoTorrent.Common;

namespace MonoTorrent.Nat
{
    public class NatManager : INatManager, IDisposable
    {
        private static NatDevice NatDevice;
        private static readonly Timer Timer = new Timer(60000);
        private static readonly Dictionary<ProtocolType, List<int>> Ports = new Dictionary<ProtocolType, List<int>>();

        public bool IsOpen { get; private set; }
        public ProtocolType Protocol { get; private set; }
        public int Port { get; private set; }

        public NatManager()
        {
            if(!Timer.Enabled)
            {
                Timer.Elapsed += OnNatExpired;
                Timer.AutoReset = true;
                Timer.Enabled = true;
            }
        }

        public NatManager(ProtocolType protocol, int port)
        {
            if(!Timer.Enabled)
            {
                Timer.Elapsed += OnNatExpired;
                Timer.AutoReset = true;
                Timer.Enabled = true;
            }

            Open(protocol, port);
        }

        private static async void FindNatDevice()
        {
            try
            {
                NatDevice = await (new NatDiscoverer()).DiscoverDeviceAsync(); //(PortMapper.Upnp, (new System.Threading.CancellationTokenSource(10000)));
            }
            catch (NatDeviceNotFoundException ex)
            {
                Trace.WriteLine("Unable to find NAT device: " + ex.Message);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("NAT failed: " + ex.Message);
            }
        }

        private static async void OpenNatDevice(ProtocolType protocol, int port)
        {
            try
            {
                switch (protocol)
                {
                    case ProtocolType.Tcp: 
                        await NatDevice.CreatePortMapAsync(new Mapping(MonoTorrent.Nat.Protocol.Tcp, port, port, (int)Timer.Interval / 1000 * 2, "MonoTorrent"));
                        break;
                    case ProtocolType.Udp: 
                        await NatDevice.CreatePortMapAsync(new Mapping(MonoTorrent.Nat.Protocol.Udp, port, port, (int)Timer.Interval / 1000 * 2, "MonoTorrent"));
                        break;
                }
            }
            catch (MappingException ex)
            {
                Trace.WriteLine("Unable to open NAT device: " + ex.Message);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("NAT failed: " + ex.Message);
            }
        }

        private static async void CloseNatDevice(ProtocolType protocol, int port)
        {
            try
            {
                switch (protocol)
                {
                    case ProtocolType.Tcp: 
                        await NatDevice.DeletePortMapAsync(new Mapping(MonoTorrent.Nat.Protocol.Tcp, port, port, (int)Timer.Interval / 1000 * 2, "MonoTorrent"));
                        break;
                    case ProtocolType.Udp: 
                        await NatDevice.DeletePortMapAsync(new Mapping(MonoTorrent.Nat.Protocol.Udp, port, port, (int)Timer.Interval / 1000 * 2, "MonoTorrent"));
                        break;
                }
            }
            catch (MappingException ex)
            {
                Trace.WriteLine("Unable to close NAT device: " + ex.Message);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("NAT failed: " + ex.Message);
            }
        }

        private static void OnNatExpired(Object source, ElapsedEventArgs e)
        {
            FindNatDevice();

            foreach (int port in Ports[ProtocolType.Tcp].Distinct().ToList())
                OpenNatDevice(ProtocolType.Tcp, port);

            foreach (int port in Ports[ProtocolType.Udp].Distinct().ToList())
                OpenNatDevice(ProtocolType.Udp, port);
        }

        public void Open(ProtocolType protocol, int port)
        {
            if(IsOpen && Protocol == protocol && Port == port)
                return;

            if(IsOpen)
                Close();

            Protocol = protocol;
            Port = port;

            if(!Ports.ContainsKey(Protocol))
                Ports.Add(Protocol, new List<int>());

            if(!Ports[Protocol].Contains(Port))
                OpenNatDevice(Protocol, Port);

            Ports[Protocol].Add(Port);

            IsOpen = true;
        }

        public void Close()
        {
            if(!IsOpen)
                return;

            Ports[Protocol].Remove(Port);

            if(!Ports[Protocol].Contains(Port))
                CloseNatDevice(Protocol, Port);

            IsOpen = false;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
#endif
