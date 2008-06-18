#if STATS

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using log4net;
using SampleClient.Stats;

namespace SampleClient
{
    delegate void InvokeDelegate(Invoke invoke);

    struct Invoke
    {
        public Delegate Action;
        public object[] Args;

        public String StackTrace;

        public Invoke(Delegate method, params object[] args)
        {
            this.Action = method;
            this.Args = args;
            this.StackTrace = Environment.StackTrace;
        }
    }


    class Utils
    {
        /// <summary>
        /// Performs the action on the control by calling BeginInvoke, if it is required to do so
        /// </summary>
        /// <param name="control"></param>
        /// <param name="action"></param>
        public static void PerformControlOperation(Control control, NoParam action)
        {
            if (control.InvokeRequired)
                control.BeginInvoke(new InvokeDelegate(TryCatchInvoker), new Invoke(action, null));
            else
                TryCatchInvoker(new Invoke(action, null));
        }


        private static void TryCatchInvoker(Invoke invoke)
        {
            try
            {
                invoke.Action.DynamicInvoke(invoke.Args);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error: " + e.Message);
                
                ILog error = log4net.LogManager.GetLogger("error");
                error.Error("Exception in invoke: ", e);
                error.ErrorFormat("Method: {0}", invoke.Action.Method);
                error.ErrorFormat("Pre-invoke stacktrace: {0}", invoke.StackTrace);

                // re-throw the exception
                throw;
            }
        }
    }
}
#endif