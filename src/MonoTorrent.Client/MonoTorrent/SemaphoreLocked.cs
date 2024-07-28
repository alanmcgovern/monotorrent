using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ReusableTasks;

namespace MonoTorrent
{
    class SemaphoreLocked
    {
        internal static SemaphoreLocked<T> Create<T> (T value)
        {
            return SemaphoreLocked<T>.Create (value);
        }
    }

    class SemaphoreLocked<T>
    {
        ReusableSemaphore Locker = new ReusableSemaphore (1);

        T Value { get; }

        internal static SemaphoreLocked<T> Create (T value)
        {
            return new SemaphoreLocked<T> (value);
        }

        SemaphoreLocked (T value)
        {
            Value = value;
        }

        public async ReusableTask<Accessor> EnterAsync ()
        {
            return new Accessor (Value, await Locker.EnterAsync ());
        }

        public readonly struct Accessor : IDisposable
        {
            public T Value { get; }
            ReusableSemaphore.Releaser InnerReleaser { get; }

            internal Accessor (T value, ReusableSemaphore.Releaser releaser)
                => (Value, InnerReleaser) = (value, releaser);

            public void Dispose ()
                => InnerReleaser.Dispose ();
        }
    }

}
