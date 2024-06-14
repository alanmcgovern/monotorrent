//
// MonoNatPortForwarder.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2020 Alan McGovern
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Mono.Nat;

namespace MonoTorrent.PortForwarding
{
    public class MonoNatPortForwarder : IPortForwarder
    {

        public event EventHandler? MappingsChanged;

        public bool Active => NatUtility.IsSearching;

        IReadOnlyList<INatDevice> Devices { get; set; }

        SemaphoreSlim Locker { get; } = new SemaphoreSlim (1);

        public Mappings Mappings { get; private set; }

        public MonoNatPortForwarder ()
        {
            Devices = new List<INatDevice> ();
            Mappings = Mappings.Empty;

            NatUtility.DeviceFound += async (o, e) => {
                using (await Locker.EnterAsync ()) {
                    if (Devices.Contains (e.Device))
                        return;
                    Devices = Devices.Concat (new[] { e.Device }).ToArray ();
                }

                foreach (var mapping in Mappings.Pending)
                    await CreateOrFailMapping (e.Device, mapping);

                RaiseMappingsChangedAsync ();
            };
        }

        public async Task RegisterMappingAsync (Mapping mapping)
        {
            using (await Locker.EnterAsync ()) {
                Mappings = Mappings.WithPending (mapping);
                if (!Active)
                    return;

                foreach (var device in Devices)
                    await CreateOrFailMapping (device, mapping);
            }
            RaiseMappingsChangedAsync ();
        }

        public async Task UnregisterMappingAsync (Mapping mapping, CancellationToken token)
        {
            using (await Locker.EnterAsync ()) {
                Mappings = Mappings.Remove (mapping, out bool wasCreated);
                if (!Active)
                    return;

                if (wasCreated) {
                    foreach (var device in Devices) {
                        token.ThrowIfCancellationRequested ();
                        await DeletePortMapping (device, mapping);
                    }
                }
            }
            RaiseMappingsChangedAsync ();
        }

        public async Task StartAsync (CancellationToken token)
        {
            using (await Locker.EnterAsync ()) {
                if (!Active) {
                    await new ThreadSwitcher ();
                    NatUtility.StartDiscovery (NatProtocol.Pmp, NatProtocol.Upnp);
                }
            }
        }

        public Task StopAsync (CancellationToken token)
            => StopAsync (true, token);

        public async Task StopAsync (bool removeExisting, CancellationToken token)
        {
            using (await Locker.EnterAsync ()) {

                NatUtility.StopDiscovery ();

                var created = Mappings.Created;
                Mappings = Mappings.WithAllPending ();
                try {
                    if (removeExisting) {
                        foreach (var mapping in created) {
                            foreach (var device in Devices) {
                                token.ThrowIfCancellationRequested ();
                                await DeletePortMapping (device, mapping);
                            }
                        }
                    }
                } finally {
                    Devices = new List<INatDevice> ();
                    RaiseMappingsChangedAsync ();
                }
            }
        }

        async Task CreateOrFailMapping (INatDevice device, Mapping mapping)
        {
            var map = new Mono.Nat.Mapping (
                mapping.Protocol == Protocol.Tcp ? Mono.Nat.Protocol.Tcp : Mono.Nat.Protocol.Udp,
                mapping.PrivatePort,
                mapping.PublicPort,
                0,
                $"int. {mapping.PrivatePort} -> ext. {mapping.PublicPort} {mapping.Protocol}"
            );

            try {
                await device.CreatePortMapAsync (map);
                Mappings = Mappings.WithCreated (mapping);
            } catch {
                Mappings = Mappings.WithFailed (mapping);
            }
        }

        async Task DeletePortMapping (INatDevice device, Mapping mapping)
        {
            var map = new Mono.Nat.Mapping (
                mapping.Protocol == Protocol.Tcp ? Mono.Nat.Protocol.Tcp : Mono.Nat.Protocol.Udp,
                mapping.PrivatePort,
                mapping.PublicPort,
                0,
                $"int. {mapping.PrivatePort} -> ext. {mapping.PublicPort} {mapping.Protocol}"
            );

            try {
                await device.DeletePortMapAsync (map);
            } catch {
            }
        }

        async void RaiseMappingsChangedAsync ()
        {
            if (MappingsChanged != null) {
                await new ThreadSwitcher ();
                MappingsChanged.Invoke (this, EventArgs.Empty);
            }
        }
    }
}
