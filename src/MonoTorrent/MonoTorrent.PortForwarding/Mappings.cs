//
// Mappings.cs
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

namespace MonoTorrent.PortForwarding
{
    public sealed class Mappings
    {
        public static readonly Mappings Empty = new Mappings ();

        /// <summary>
        /// A list of mappings which have been successfully created
        /// </summary>
        public IReadOnlyList<Mapping> Created { get; }

        /// <summary>
        /// A list of mappings which will be created as soon as a compatible uPnP or NAT-PMP router
        /// is discovered.
        /// </summary>
        public IReadOnlyList<Mapping> Pending { get; }

        /// <summary>
        /// A list of mappings which could not be created. This can happen if the public port is already
        /// in use and is mapped to a different IP address in the local network.
        /// </summary>
        public IReadOnlyList<Mapping> Failed { get; }

        public Mappings ()
        {
            Created = Pending = Failed = Array.AsReadOnly (Array.Empty<Mapping> ());
        }

        Mappings (IReadOnlyList<Mapping> created, IReadOnlyList<Mapping> pending, IReadOnlyList<Mapping> failed)
        {
            Created = created;
            Pending = pending;
            Failed = failed;
        }

        public Mappings Remove (Mapping mapping, out bool wasCreated)
        {
            wasCreated = Created.Contains (mapping);
            var created = Created.Contains (mapping) ? Created.Except (new[] { mapping }).ToArray () : Created;
            var pending = Pending.Contains (mapping) ? Pending.Except (new[] { mapping }).ToArray () : Pending;
            var failed = Failed.Contains (mapping) ? Failed.Except (new[] { mapping }).ToArray () : Failed;

            return new Mappings (created, pending, failed);
        }

        public Mappings WithAllPending ()
        {
            return new Mappings (Array.Empty<Mapping> (), Array.AsReadOnly (Created.Concat (Pending).Concat (Failed).ToArray ()), Array.Empty<Mapping> ());
        }

        public Mappings WithCreated (Mapping mapping)
        {
            var created = !Created.Contains (mapping) ? Array.AsReadOnly (Created.Concat (new[] { mapping }).ToArray ()) : Created;
            var failed = Failed.Contains (mapping) ? Array.AsReadOnly (Failed.Where (t => t != mapping).ToArray ()) : Failed;
            var pending = Pending.Contains (mapping) ? Array.AsReadOnly (Pending.Where (t => t != mapping).ToArray ()) : Pending;

            return new Mappings (created, pending, failed);
        }

        public Mappings WithFailed (Mapping mapping)
        {
            var created = Created.Contains (mapping) ? Array.AsReadOnly (Created.Where (t => t != mapping).ToArray ()) : Created;
            var failed = !Failed.Contains (mapping) ? Array.AsReadOnly (Failed.Concat (new[] { mapping }).ToArray ()) : Failed;
            var pending = Pending.Contains (mapping) ? Array.AsReadOnly (Pending.Where (t => t != mapping).ToArray ()) : Pending;

            return new Mappings (created, pending, failed);
        }

        public Mappings WithPending (Mapping mapping)
        {
            var created = Created.Contains (mapping) ? Array.AsReadOnly (Created.Where (t => t != mapping).ToArray ()) : Created;
            var failed = Failed.Contains (mapping) ? Array.AsReadOnly (Failed.Where (t => t != mapping).ToArray ()) : Failed;
            var pending = !Pending.Contains (mapping) ? Array.AsReadOnly (Pending.Concat (new[] { mapping }).ToArray ()) : Pending;

            return new Mappings (created, pending, failed);
        }
    }
}
