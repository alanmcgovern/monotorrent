using System;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        public ClientEngine Engine { get; }

        public Exception Exception { get; }


        public CriticalExceptionEventArgs (Exception ex, ClientEngine engine)
        {
            if (ex == null)
                throw new ArgumentNullException (nameof(ex));
            if (engine == null)
                throw new ArgumentNullException (nameof(engine));

            this.Engine = engine;
            this.Exception = ex;
        }
    }
}
