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
        public async Task IncreaseSemaphoreCountNonePending ()
        {
            var semaphore = new ReusableSemaphore (4);

            var tasks = new List<Task<ReusableSemaphore.Releaser>> {
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
            };

            // Release 4 more!
            semaphore.ChangeCount (8);
            for (int i = 0; i < 5; i++)
                await semaphore.EnterAsync ().WithTimeout ();
        }

        [Test]
        public async Task IncreaseSemaphoreCountWithPending ()
        {
            var semaphore = new ReusableSemaphore (2);

            var tasks = new List<Task<ReusableSemaphore.Releaser>> {
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
            };

            // 2 should be ready ~immediately!
            var first = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (first);

            var second = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (second);

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));

            // Release two more!
            semaphore.ChangeCount (4);
            var third = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (third);

            // We should get another idle task
            var fourth = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (fourth);

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));

            // Complete the first task
            (await first).Dispose ();

            // One more dequeues
            var fifth = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (fifth);
        }

        [Test]
        public async Task DecreaseSemaphoreCountNonePending ()
        {
            var semaphore = new ReusableSemaphore (6);

            var tasks = new List<Task<ReusableSemaphore.Releaser>> {
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
            };

            // 1 more than are currently running
            semaphore.ChangeCount (4);
            // The same as are currently running
            semaphore.ChangeCount (3);
            // 1 fewer than are currently running
            semaphore.ChangeCount (2);

            foreach (var task in tasks)
                (await task).Dispose ();
        }

        [Test]
        public async Task DecreaseSemaphoreCountWithPending ()
        {
            var semaphore = new ReusableSemaphore (3);

            var tasks = new List<Task<ReusableSemaphore.Releaser>> {
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
                semaphore.EnterAsync ().AsTask (),
            };

            // 3 should be ready ~immediately!
            var first = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (first);

            var second = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (second);

            var third = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (third);

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));

            // Change count to 1 and release 2 tasks
            semaphore.ChangeCount (1);

            // Complete the first two tasks
            (await first).Dispose ();
            (await second).Dispose ();

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));

            // Complete the pending task, one more should begin
            (await third).Dispose ();

            var fourth = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (fourth);

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));

            (await fourth).Dispose ();

            var fifth = await Task.WhenAny (tasks).WithTimeout ();
            tasks.Remove (fifth);

            // None of the rest should be ready
            Assert.AreEqual (-1, Task.WaitAny (tasks.ToArray (), 100));
        }

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
