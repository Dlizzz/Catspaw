using System;
using System.Globalization;
using Nancy.Hosting.Self;
using Serilog;
using Catspaw.Properties;

namespace Catspaw.Api
{
    /// <summary>
    /// Implement the catspaw self-hosted api server
    /// </summary>
    public class ApiServer: IDisposable
    {
        // Api web server
        private readonly NancyHost apiServer;

        /// <summary>Create and start the Api server</summary>
        public ApiServer()
        {
            var apiServerConfig = new HostConfiguration
            {
                UrlReservations = new UrlReservations() { CreateAutomatically = true }
            };
            
            // Warning: Uri must terminate with / or server will crash when starting
            BaseUri = new Uri(
                "http://localhost:"
                + Settings.Default.ApiServerPort.ToString(CultureInfo.InvariantCulture)
                + "/" + Resources.StrApiServerRoot + "/");
            
            apiServer = new NancyHost(apiServerConfig, BaseUri);
            
            apiServer.Start();
            Log.Debug("Api server started on " + BaseUri.ToString());
            Log.Information("I'm fully ready. I'm now carefully listening on " + BaseUri.ToString());
        }

        /// <summary>The base Uri for the Api server</summary>
        /// <value cref="Uri">The Uri</value>
        public Uri BaseUri { get; }

        #region IDisposable Support
        // Avoid redundant calls
        private bool disposedValue = false;

        /// <summary>
        /// Do dispose the resources if disposing is true
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    apiServer?.Stop();
                    apiServer.Dispose();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Implement IDisposable interface
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
