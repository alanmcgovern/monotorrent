using MonoTorrent.Tracker;

namespace MonoTorrent.Tests.Tracker
{
    public class CustomComparer : IPeerComparer
    {
        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.Uploaded;
        }

        public new bool Equals(object left, object right)
        {
            return left.Equals(right);
        }

        public int GetHashCode(object obj)
        {
            return obj.GetHashCode();
        }
    }
}