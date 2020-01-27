using System;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        public ClientEngine Engine { get; }

        public Exception Exception { get; }


        public CriticalExceptionEventArgs (Exception ex, ClientEngine engine)
        {
            this.Engine = engine ?? throw new ArgumentNullException (nameof(engine));
            this.Exception = ex ?? throw new ArgumentNullException (nameof(ex));
        }
    }
}
