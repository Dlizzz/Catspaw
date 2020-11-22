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
            Get(@"^(?:vol(?<ratio>r?)(?<action>[ud])(?<amount>[\d]{0,2}))$", VolumeSet);
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

        private dynamic VolumeSet(dynamic args)
        {
            bool ratio = (args.ratio == "r");
            string action = args.action;
            // If no amount given, could be 0% or standard up or down (0.5 dB)
            double amount = (args.amount == "") ? 0.0 : args.amount.ToDouble(CultureInfo.InvariantCulture);

            string message;

            if (ratio)
            {
                message = action switch
                {
                    "u" => "Volume up by " + amount.ToString(CultureInfo.InvariantCulture) + "%",
                    "d" => "Volume down by " + amount.ToString(CultureInfo.InvariantCulture) + "%",
                    _ => "No action",
                };
            }
            else
            {
                var strAmount = (args.amount == "") ? "0.5" : amount.ToString(CultureInfo.InvariantCulture);

                message = action switch
                {
                    "u" => "Volume up by " + strAmount + " dB",
                    "d" => "Volume down by " + strAmount + " dB",
                    _ => "No action",
                };
            }

            // Call Avr command
            Log.Debug("Api: " + message);
            try
            {
                ((App)Application.Current).PioneerAvr?.VolumeSet(action, amount, ratio);
            }
            catch (AvrException err)
            {
                Log.Debug("Api: " + message + " failed", err);
            }

            return message;
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
