using NUnit.Framework;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class CacheTests
    {
        class Cacheable : ICacheable
        {
            public int Initialised { get; set; }

            public void Initialise ()
            {
                Initialised++;
            }
        }

        [Test]
        public void Dequeue_WithAutocreate ()
        {
            var cache = new SynchronizedCache<Cacheable> (() => new Cacheable ());
            Assert.AreEqual (0, cache.Count);
            Assert.IsTrue (cache.Dequeue ().Initialised == 1);
        }

        [Test]
        public void UseTwice ()
        {
            // Should be initialised twice.
            var cache = new Cache<Cacheable> (() => new Cacheable ());
            var item = cache.Dequeue ();
            Assert.AreEqual (1, item.Initialised);

            cache.Enqueue (item);
            Assert.AreEqual (2, item.Initialised);

            Assert.AreSame (item, cache.Dequeue ());
            Assert.AreEqual (2, item.Initialised);
        }
    }
}
