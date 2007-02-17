using System;
using System.Collections.Generic;
using System.Text;
using Nat;
using System.Diagnostics;

namespace MonoTorrent.Client
{
    internal class PortMapper : IDisposable
    {
        private NatController controller;
        private INatDevice router;
        private int port;

        public event EventHandler RouterFound;

        internal PortMapper()
        {
            this.controller = new NatController();
            this.controller.DeviceFound += new EventHandler<DeviceEventArgs>(controller_DeviceFound);
            this.port = -1;
        }

        private void controller_DeviceFound(object sender, DeviceEventArgs e)
        {
            if (router == null)
            {
                this.router = e.Device;
                if (this.RouterFound != null)
                    this.RouterFound(this, EventArgs.Empty);
            }
        }

        internal void Start()
        {
            this.controller.StartSearching();
        }

        internal void MapPort(int port)
        {
            this.port = port;
            this.router.BeginCreatePortMap(new Mapping((ushort)port, Protocol.Tcp), "", new AsyncCallback(EndMapPort), null);
        }

        private void EndMapPort(IAsyncResult result)
        {
            try
            {
                this.router.EndCreatePortMap(result);
                Debug.WriteLine("Port mapped: " + this.port);
            }
            catch
            {
                Debug.WriteLine("Couldn't map the port: " + this.port);
            }
        }


        public void Dispose()
        {
            this.router.DeletePortMap(new Mapping((ushort)this.port, Protocol.Tcp));
            this.controller.Dispose();
        }
    }
}