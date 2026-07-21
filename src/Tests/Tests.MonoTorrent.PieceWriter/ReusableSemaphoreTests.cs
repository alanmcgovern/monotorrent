using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace MonoTorrent.PieceWriter
{
    [TestFixture]
    public class ReusableSemaphoreTests
    {
        [Test]
        public void TryEnter ()
        {
            var semaphore = new ReusableSemaphore (4);
            for (int i = 0; i < semaphore.Count; i++)
                Assert.IsTrue (semaphore.TryEnter (out _));

            Assert.IsFalse (semaphore.TryEnter (out _));
            Assert.IsFalse (semaphore.TryEnter (out _));
        }
    }
}
