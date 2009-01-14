using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class RangeCollectionTests
    {
        //static void Main()
        //{
        //    RangeCollectionTests t = new RangeCollectionTests();
        //    t.AddTest();
        //    t.AddTest2();
        //    t.AddTest3();
        //}
        [Test]
        public void AddTest()
        {
            RangeCollection c = new RangeCollection();
            c.Add(new AddressRange(50, 50));
            c.Add(new AddressRange(50, 50));
            Assert.AreEqual(1, c.Ranges.Count, "#1");
            c.Add(new AddressRange(50, 51));
            Assert.AreEqual(1, c.Ranges.Count, "#2");
            c.Add(new AddressRange(51, 51));
            Assert.AreEqual(new AddressRange(50, 51), c.Ranges[0], "#2b");
            c.Add(new AddressRange(50, 50));
            c.Add(new AddressRange(49, 50));
            Assert.AreEqual(1, c.Ranges.Count, "#3");
            Assert.AreEqual(new AddressRange(49, 51), c.Ranges[0], "#3b");
            c.Add(new AddressRange(45, 47));
            Assert.AreEqual(2, c.Ranges.Count, "#4");
            Assert.AreEqual(new AddressRange(49, 51), c.Ranges[1], "#4b");
            c.Add(new AddressRange(47, 49));
            Assert.AreEqual(1, c.Ranges.Count, "#5");
            Assert.AreEqual(new AddressRange(45, 51), c.Ranges[0], "#4b");
        }

        [Test]
        public void AddTest2()
        {
            RangeCollection c = new RangeCollection();
            List<AddressRange> ranges = c.Ranges;
            c.Add(new AddressRange(0, 100));
            c.Add(new AddressRange(101, 200));
            Assert.AreEqual(1, ranges.Count, "#1");
            Assert.AreEqual(new AddressRange(0, 200), ranges[0], "#1b");
            c.Add(new AddressRange(300, 400));
            c.Add(new AddressRange(500, 600));
            Assert.AreEqual(3, ranges.Count, "#2");
            c.Add(new AddressRange(50, 205));
            Assert.AreEqual(3, ranges.Count, "#3");
            Assert.AreEqual(new AddressRange(0, 205), ranges[0], "#3b");
            c.Add(new AddressRange(-100, -1));
            Assert.AreEqual(3, ranges.Count, "#4");
            Assert.AreEqual(new AddressRange(-100, 205), ranges[0], "#4b");
            c.Add(new AddressRange(206, 299));
            Assert.AreEqual(2, ranges.Count, "#5");
            Assert.AreEqual(new AddressRange(-100, 400), ranges[0], "#5b");
            c.Add(new AddressRange(0, 600));
            Assert.AreEqual(1, ranges.Count, "#6");
            Assert.AreEqual(new AddressRange(-100, 600), ranges[0], "#6b");
        }

        [Test]
        public void AddTest3()
        {
            RangeCollection c = new RangeCollection();
            List<AddressRange> ranges = c.Ranges;
            c.Add(new AddressRange(0, 100));
            c.Add(new AddressRange(200, 300));
            c.Add(new AddressRange(150, 600));
            Assert.AreEqual(2, ranges.Count, "#1");
            Assert.AreEqual(new AddressRange(0, 100), ranges[0], "#1b");
            Assert.AreEqual(new AddressRange(150, 600), ranges[1], "#1c");
            c.Add(new AddressRange(102, 500));
            Assert.AreEqual(2, ranges.Count, "#2");
            Assert.AreEqual(new AddressRange(102, 600), ranges[1], "#2b");
            c.Add(new AddressRange(400, 700));
            Assert.AreEqual(2, ranges.Count, "#3");
            Assert.AreEqual(new AddressRange(102, 700), ranges[1], "#3b");
        }
    }
}
