using System;
using System.Net;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker.Listeners
{
    public class HttpListener : ListenerBase
    {
        #region Properties

        /// <summary>
        ///     True if the listener is waiting for incoming connections
        /// </summary>
        public override bool Running
        {
            get { return listener != null; }
        }

        #endregion Properties

        #region Fields

        private readonly string prefix;
        private System.Net.HttpListener listener;

        #endregion Fields

        #region Constructors

        public HttpListener(IPAddress address, int port)
            : this(string.Format("http://{0}:{1}/announce/", address, port))
        {
        }

        public HttpListener(IPEndPoint endpoint)
            : this(endpoint.Address, endpoint.Port)
        {
        }

        public HttpListener(string httpPrefix)
        {
            if (string.IsNullOrEmpty(httpPrefix))
                throw new ArgumentNullException("httpPrefix");

            prefix = httpPrefix;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        ///     Starts listening for incoming connections
        /// </summary>
        public override void Start()
        {
            if (Running)
                return;

            listener = new System.Net.HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            listener.BeginGetContext(EndGetRequest, listener);
        }

        /// <summary>
        ///     Stops listening for incoming connections
        /// </summary>
        public override void Stop()
        {
            if (!Running)
                return;

            var d = (IDisposable) listener;
            listener = null;
            d.Dispose();
        }

        private void EndGetRequest(IAsyncResult result)
        {
            HttpListenerContext context = null;
            var listener = (System.Net.HttpListener) result.AsyncState;

            try
            {
                context = listener.EndGetContext(result);
                using (context.Response)
                    HandleRequest(context);
            }
            catch (Exception ex)
            {
                Console.Write("Exception in listener: {0}{1}", Environment.NewLine, ex);
            }
            finally
            {
                try
                {
                    if (listener.IsListening)
                        listener.BeginGetContext(EndGetRequest, listener);
                }
                catch
                {
                    Stop();
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var isScrape = context.Request.RawUrl.StartsWith("/scrape", StringComparison.OrdinalIgnoreCase);

            BEncodedValue responseData = Handle(context.Request.RawUrl, context.Request.RemoteEndPoint.Address, isScrape);

            var response = responseData.Encode();
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = response.LongLength;
            context.Response.OutputStream.Write(response, 0, response.Length);
        }

        #endregion Methods
    }
}