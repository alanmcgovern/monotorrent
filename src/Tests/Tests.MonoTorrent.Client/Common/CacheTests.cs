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
        public void Dequeue_NoAutocreate ()
        {
            var cache = new Cache<Cacheable> ();
            Assert.AreEqual (0, cache.Count);
            Assert.IsNull (cache.Dequeue ());
        }

        [Test]
        public void Dequeue_WithAutocreate ()
        {
            var cache = new Cache<Cacheable> (true).Synchronize ();
            Assert.AreEqual (0, cache.Count);
            Assert.IsTrue (cache.Dequeue ().Initialised == 1);
        }

        [Test]
        public void Enqueue ()
        {
            var item = new Cacheable ();
            var cache = new Cache<Cacheable> ();
            cache.Enqueue (item);
            Assert.AreEqual (1, cache.Count);
            Assert.AreEqual (1, item.Initialised);
        }

        [Test]
        public void UseTwice ()
        {
            // Should be initialised twice.
            var cache = new Cache<Cacheable> (true);
            var item = cache.Dequeue ();
            Assert.AreEqual (1, item.Initialised);

            cache.Enqueue (item);
            Assert.AreEqual (2, item.Initialised);

            Assert.AreSame (item, cache.Dequeue ());
            Assert.AreEqual (2, item.Initialised);
        }
    }
}
