using Xunit;

namespace MonoTorrent.Client
{
    public class RangeCollectionTests
    {
        //static void Main()
        //{
        //    RangeCollectionTests t = new RangeCollectionTests();
        //    t.AddTest();
        //    t.AddTest2();
        //    t.AddTest3();
        //}
        [Fact]
        public void AddTest()
        {
            var c = new RangeCollection();
            c.Add(new AddressRange(50, 50));
            c.Add(new AddressRange(50, 50));
            Assert.Equal(1, c.Ranges.Count);
            c.Add(new AddressRange(50, 51));
            Assert.Equal(1, c.Ranges.Count);
            c.Add(new AddressRange(51, 51));
            Assert.Equal(new AddressRange(50, 51), c.Ranges[0]);
            c.Add(new AddressRange(50, 50));
            c.Add(new AddressRange(49, 50));
            Assert.Equal(1, c.Ranges.Count);
            Assert.Equal(new AddressRange(49, 51), c.Ranges[0]);
            c.Add(new AddressRange(45, 47));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(49, 51), c.Ranges[1]);
            c.Add(new AddressRange(47, 49));
            Assert.Equal(1, c.Ranges.Count);
            Assert.Equal(new AddressRange(45, 51), c.Ranges[0]);
        }

        [Fact]
        public void AddTest2()
        {
            var c = new RangeCollection();
            var ranges = c.Ranges;
            c.Add(new AddressRange(0, 100));
            c.Add(new AddressRange(101, 200));
            Assert.Equal(1, ranges.Count);
            Assert.Equal(new AddressRange(0, 200), ranges[0]);
            c.Add(new AddressRange(300, 400));
            c.Add(new AddressRange(500, 600));
            Assert.Equal(3, ranges.Count);
            c.Add(new AddressRange(50, 205));
            Assert.Equal(3, ranges.Count);
            Assert.Equal(new AddressRange(0, 205), ranges[0]);
            c.Add(new AddressRange(-100, -1));
            Assert.Equal(3, ranges.Count);
            Assert.Equal(new AddressRange(-100, 205), ranges[0]);
            c.Add(new AddressRange(206, 299));
            Assert.Equal(2, ranges.Count);
            Assert.Equal(new AddressRange(-100, 400), ranges[0]);
            c.Add(new AddressRange(0, 600));
            Assert.Equal(1, ranges.Count);
            Assert.Equal(new AddressRange(-100, 600), ranges[0]);
        }

        [Fact]
        public void AddTest3()
        {
            var c = new RangeCollection();
            var ranges = c.Ranges;
            c.Add(new AddressRange(0, 100));
            c.Add(new AddressRange(200, 300));
            c.Add(new AddressRange(150, 600));
            Assert.Equal(2, ranges.Count);
            Assert.Equal(new AddressRange(0, 100), ranges[0]);
            Assert.Equal(new AddressRange(150, 600), ranges[1]);
            c.Add(new AddressRange(102, 500));
            Assert.Equal(2, ranges.Count);
            Assert.Equal(new AddressRange(102, 600), ranges[1]);
            c.Add(new AddressRange(400, 700));
            Assert.Equal(2, ranges.Count);
            Assert.Equal(new AddressRange(102, 700), ranges[1]);
        }

        [Fact]
        public void ContainsTest()
        {
            var c = new RangeCollection();
            c.Add(new AddressRange(1, 100));
            c.Add(new AddressRange(-10, -1));
            for (var i = -15; i < 120; i++)
            {
                var shouldContain = (i >= -10 && i <= -1) || (i >= 1 && i <= 100);
                Assert.Equal(shouldContain, c.Contains(new AddressRange(i, i)));
            }
        }

        [Fact]
        public void RemoveTest()
        {
            var c = new RangeCollection();
            c.Add(new AddressRange(0, 100));
            c.Remove(new AddressRange(50, 50));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(0, 49), c.Ranges[0]);
            Assert.Equal(new AddressRange(51, 100), c.Ranges[1]);

            c.Remove(new AddressRange(50, 55));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(0, 49), c.Ranges[0]);
            Assert.Equal(new AddressRange(56, 100), c.Ranges[1]);

            c.Remove(new AddressRange(45, 60));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(0, 44), c.Ranges[0]);
            Assert.Equal(new AddressRange(61, 100), c.Ranges[1]);

            c.Remove(new AddressRange(45, 60));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(0, 44), c.Ranges[0]);
            Assert.Equal(new AddressRange(61, 100), c.Ranges[1]);

            c.Remove(new AddressRange(-100, 5));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(6, 44), c.Ranges[0]);
            Assert.Equal(new AddressRange(61, 100), c.Ranges[1]);

            c.Remove(new AddressRange(6, 15));
            Assert.Equal(2, c.Ranges.Count);
            Assert.Equal(new AddressRange(16, 44), c.Ranges[0]);
            Assert.Equal(new AddressRange(61, 100), c.Ranges[1]);

            c.Remove(new AddressRange(70, 80));
            Assert.Equal(3, c.Ranges.Count);
            Assert.Equal(new AddressRange(16, 44), c.Ranges[0]);
            Assert.Equal(new AddressRange(61, 69), c.Ranges[1]);
            Assert.Equal(new AddressRange(81, 100), c.Ranges[2]);
        }
    }
}