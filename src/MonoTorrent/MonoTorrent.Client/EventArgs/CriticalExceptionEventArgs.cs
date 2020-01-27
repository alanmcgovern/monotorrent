using System;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        private readonly ClientEngine engine;
        private readonly Exception ex;


        public ClientEngine Engine {
            get { return engine; }
        }

        public Exception Exception {
            get { return ex; }
        }


        public CriticalExceptionEventArgs (Exception ex, ClientEngine engine)
        {
            if (ex == null)
                throw new ArgumentNullException (nameof(ex));
            if (engine == null)
                throw new ArgumentNullException (nameof(engine));

            this.engine = engine;
            this.ex = ex;
        }
    }
}
