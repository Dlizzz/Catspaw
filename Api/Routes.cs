using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Serilog;
using Catspaw.Properties;

namespace Catspaw.Api
{
    /// <summary>Define the catspaw api</summary>
    public class Routes : NancyModule
    {
        /// <summary>
        /// Create the catspaw apî with default version number
        /// </summary>
        public Routes() : base("/api/" + Resources.StrApiVersion)
        {
            Get("/version", _ => Assembly.GetExecutingAssembly().FullName);
            Get("/poweroff", PowerOff);
        }

        private dynamic PowerOff(dynamic args)
        {
            Log.Debug("Api: PowerOff");
            Log.Information("I just recieved an external request to go to bed...");

            // Run an asynchronous task with somme delay to give server time to return
            var t = Task.Run(() => { Thread.Sleep(100); _ = NativeMethods.SetSuspendState(false, false, false); });

            return "Putting system in suspend mode";
        }
    }
}
