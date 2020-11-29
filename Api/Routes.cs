using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Globalization;
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
            Get(@"^(?:vol(?<action>[ud]))$", VolumeSetAsync);
            Get("/mute", MuteOnOffAsync);
        }

        private dynamic PowerOff(dynamic args)
        {
            Log.Debug("Api: ================== PowerOff ==================");
            Log.Information("I just recieved an external request to go to bed...");

            // Run an asynchronous task with some delay to give server time to return
            var t = Task.Run(() => { Thread.Sleep(100); _ = NativeMethods.SetSuspendState(false, false, false); });

            return "Putting system in suspend mode";
        }

        private async Task<dynamic> VolumeSetAsync(dynamic args)
        {
            string message = args.action switch
            {
                "u" => "Volume up by 0.5 dB",
                "d" => "Volume down by 0.5 dB",
                _ => "No action",
            };

            // Call Avr command
            Log.Debug("Api: ================== " + message + " ==================");
            try
            {
                await (((App)Application.Current).PioneerAvr?.VolumeSetAsync(args.action)).ConfigureAwait(true);
            }
            catch (AvrException err)
            {
                Log.Error("Api: " + message + " failed", err);
            }

            return message;
        }

        private async Task<dynamic> MuteOnOffAsync(dynamic args)
        {
            Log.Debug("Api: ================== MuteOnOff ==================");

            // Call Avr command
            try
            {
                await (((App)Application.Current).PioneerAvr?.MuteOnOffAsync()).ConfigureAwait(true);
            }
            catch (AvrException err)
            {
                Log.Error("Mute switch on AVR failed", err);
            }

            return "Switched system mute";
        }
    }
}
