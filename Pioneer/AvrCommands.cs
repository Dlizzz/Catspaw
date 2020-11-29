using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Catspaw.Pioneer
{
    public partial class Avr
    {
        private enum AvrCommands
        {
            PowerOn,
            PowerOff,
            VolumeUp,
            VolumeDown,
            MuteOnOff
        }

        // Initialize the actions dictionnary for the avr
        private static readonly Dictionary<AvrCommands, string> commands = new Dictionary<AvrCommands, string>
        {
            { AvrCommands.PowerOn,    "PO"},
            { AvrCommands.PowerOff,   "PF"},
            { AvrCommands.VolumeUp,   "VU"},
            { AvrCommands.VolumeDown, "VD"},
            { AvrCommands.MuteOnOff,  "MZ"}
        };

        /// <summary>
        /// Mute switch on/off.
        /// </summary>
        /// <exception cref="AvrException">
        /// Communication error with Avr.</exception>
        public async Task MuteOnOffAsync()
        {
            // Stop showing Volume popup
            volumePopupShowTimer.Stop();

            // Switch Mute
            Log.Debug("Avr command: " + AvrCommands.MuteOnOff.ToString("G"));
            await SendAsync(commands[AvrCommands.MuteOnOff]).ConfigureAwait(true);

            // Start timer to show Volume popup
            volumePopupShowTimer.Start();
        }

        /// <summary>
        /// Increase / Decrease the volume by standard step or by amount or by ratio
        /// Avr is considered connected
        /// </summary>
        /// <param name="action">'u' or 'd' action for volume up or volume down</param>
        /// <exception cref="AvrException">Communication error with Avr.</exception>
        public async Task VolumeSetAsync(string action)
        {
            // Stop showing Volume popup
            volumePopupShowTimer.Stop();

            string command = action switch
            {
                "u" => commands[AvrCommands.VolumeUp],
                "d" => commands[AvrCommands.VolumeDown],
                _ => commands[AvrCommands.VolumeDown]
            };

            // Set volume
            Log.Debug("Avr command: " + command);
            await SendAsync(command).ConfigureAwait(true);

            // Start timer to show Volume popup
            volumePopupShowTimer.Start();
        }

        /// <summary>
        /// Power off Avr. Response is not checked for performance reasons.
        /// Avr is considered connected
        /// </summary>
        /// <exception cref="AvrException">
        /// Communication error with Avr.</exception>
        public async Task PowerOffAsync() => await SendAsync(commands[AvrCommands.PowerOff]).ConfigureAwait(true);

        /// <summary>
        /// Power on Avr. Response is not checked for performance reasons.
        /// Avr connection is resetted as we have lost the connection when power was down.
        /// </summary>
        /// <exception cref="AvrException">
        /// Network timeout or communication error with Avr.</exception>
        public async Task PowerOnAsync() => await SendAsync(commands[AvrCommands.PowerOn]).ConfigureAwait(true);
    }
}
