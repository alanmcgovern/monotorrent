namespace MonoTorrent.Tracker
{
    public interface IPeerComparer
    {
        object GetKey(AnnounceParameters parameters);
    }

    public class IPAddressComparer : IPeerComparer
    {
        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.ClientAddress;
        }
    }
}