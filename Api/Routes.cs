using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Nancy;
using Serilog;
using Catspaw.Properties;
using Catspaw.Pioneer;

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
            Get("/volume/{action}", Volume);
            Get("/mute", MuteOnOff);
        }

        private dynamic PowerOff(dynamic args)
        {
            Log.Debug("Api: PowerOff");
            Log.Information("I just recieved an external request to go to bed...");

            // Run an asynchronous task with some delay to give server time to return
            var t = Task.Run(() => { Thread.Sleep(100); _ = NativeMethods.SetSuspendState(false, false, false); });

            return "Putting system in suspend mode";
        }

        private dynamic Volume(dynamic args)
        {
            string action = args.action;

            Log.Debug("Api: Volume");

            // Call Avr command
            try
            {
                switch (action)
                {
                    case "up":
                        ((App)Application.Current).PioneerAvr?.VolumeUp();
                        break;
                    case "down":
                        ((App)Application.Current).PioneerAvr?.VolumeDown();
                        break;
                    default:
                        break;
                }
            }
            catch (AvrException err)
            {
                Log.Debug("Volume " + action + " on AVR failed", err);
            }

            return "Volume " + action + " by 0.5 db";
        }

        private dynamic MuteOnOff(dynamic args)
        {
            Log.Debug("Api: MuteOnOff");

            // Call Avr command
            try
            {
                ((App)Application.Current).PioneerAvr?.MuteOnOff();
            }
            catch (AvrException err)
            {
                Log.Debug("Mute switch on AVR failed", err);
            }

            return "Switched system mute ";
        }
    }
}
