using System;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        public ClientEngine Engine { get; }

        public Exception Exception { get; }


        public CriticalExceptionEventArgs (Exception ex, ClientEngine engine)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            Exception = ex ?? throw new ArgumentNullException (nameof (ex));
        }
    }
}
