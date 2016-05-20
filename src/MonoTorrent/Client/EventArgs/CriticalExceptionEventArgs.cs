using System;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        public CriticalExceptionEventArgs(Exception ex, ClientEngine engine)
        {
            if (ex == null)
                throw new ArgumentNullException("ex");
            if (engine == null)
                throw new ArgumentNullException("engine");

            Engine = engine;
            Exception = ex;
        }


        public ClientEngine Engine { get; }

        public Exception Exception { get; }
    }
}