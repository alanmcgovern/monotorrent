//
// IPortForwarder.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace MonoTorrent.PortForwarding
{
    public interface IPortForwarder
    {
        event EventHandler MappingsChanged;

        /// <summary>
        /// True if the port forwarding is enabled
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// The list of mappings which have been registered. If the mapping was successfully established it will
        /// be in the <see cref="Mappings.Created"/> list. If an error occurred creating the mapping it will be
        /// in the <see cref="Mappings.Failed"/> list, otherwise it will be in the <see cref="Mappings.Pending"/>
        /// list.
        /// </summary>
        Mappings Mappings { get; }

        /// <summary>
        /// Forwards a port on a NAT-PMP or uPnP capable router.
        /// </summary>
        /// <param name="mapping">The mapping to try and create.</param>
        /// <returns></returns>
        Task RegisterMappingAsync (Mapping mapping);

        /// <summary>
        /// Removes a port forwarding mapping from the router.
        /// </summary>
        /// <param name="mapping">The mapping to remove from the router. to use for the external and internal port number.</param>
        /// <param name="token">If the token is cancelled then the port map may not be fully removed from the router.</param>
        /// <returns></returns>
        Task UnregisterMappingAsync (Mapping mapping, CancellationToken token);

        /// <summary>
        /// Begins searching for any compatible port forwarding devices. Refreshes any forwarded ports automatically
        /// before the mapping expires.
        /// </summary>
        /// <returns></returns>
        Task StartAsync (CancellationToken token);

        /// <summary>
        /// Removes any port map requests and stops searching for compatible port forwarding devices. Cancels any pending
        /// ForwardPort requests.
        /// </summary>
        /// <returns></returns>
        Task StopAsync (CancellationToken token);

        /// <summary>
        /// Removes any port map requests and stops searching for compatible port forwarding devices. Cancels any pending
        /// ForwardPort requests.
        /// </summary>
        /// <returns></returns>
        Task StopAsync (bool removeExistingMappings, CancellationToken token);
    }
}
