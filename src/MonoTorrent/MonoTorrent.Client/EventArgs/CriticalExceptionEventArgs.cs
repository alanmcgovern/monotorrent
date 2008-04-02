using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class CriticalExceptionEventArgs : EventArgs
    {
        private ClientEngine engine;
        private Exception ex;


        public ClientEngine Engine
        {
            get { return engine; }
        }

        public Exception Exception
        {
            get { return ex; }
        }


        public CriticalExceptionEventArgs(Exception ex, ClientEngine engine)
        {
            if (ex == null)
                throw new ArgumentNullException("ex");
            if (engine == null)
                throw new ArgumentNullException("engine");

            this.engine = engine;
            this.ex = ex;
        }
    }
}
