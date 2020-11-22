using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Serilog;

namespace Catspaw.Pioneer
{
    public partial class Avr
    {
        private const RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.Singleline;

        private enum AvrCommands 
        { 
            PowerState,
            PowerOn,
            PowerOff,
            VolumeUp,
            VolumeDown,
            VolumeSet,
            VolumeGet,
            MuteOnOff
        }

        // Initialize the actions dictionnary for the avr
        private static readonly Dictionary<AvrCommands, (string command, Regex expect)> commands = new Dictionary<AvrCommands, (string command, Regex expect)>
        {
            { AvrCommands.PowerState, ("?P",    new Regex("PWR([0-1])", regexOptions)) },
            { AvrCommands.PowerOn,    ("PO",    new Regex("PWR0", regexOptions)) },
            { AvrCommands.PowerOff,   ("PF",    new Regex("PWR1", regexOptions)) },
            { AvrCommands.VolumeUp,   ("VU",    new Regex(@"VOL(\d{3})", regexOptions)) },
            { AvrCommands.VolumeDown, ("VD",    new Regex(@"VOL(\d{3})", regexOptions)) },
            { AvrCommands.VolumeSet,  ("***VL", new Regex(@"VOL(\d{3})", regexOptions)) },
            { AvrCommands.VolumeGet,  ("?V",    new Regex(@"VOL(\d{3})", regexOptions)) },
            { AvrCommands.MuteOnOff,  ("MZ",    new Regex("", regexOptions)) }
        };

        /// <summary>
        /// Mute switch on/off. Response is not checked for performance reasons.
        /// Avr is considered connected
        /// </summary>
        /// <exception cref="AvrException">
        /// Communication error with Avr.</exception>
        public void MuteOnOff() => Send(commands[AvrCommands.MuteOnOff].command);

        /// <summary>
        /// Increase / Decrease the volume by standard step or by amount or by ratio
        /// Avr is considered connected
        /// </summary>
        /// <param name="action">'u' or 'd' action for volume up or volume down</param>
        /// <param name="amount">volume amount to add or sustract or ratio if ratio is true</param>
        /// <param name="ratio">true if amount is a ratio to add or substract</param>
        /// <exception cref="AvrException">Communication error with Avr.</exception>
        public void VolumeSet(string action, double amount, bool ratio)
        {
            string response;
            
            if (ratio || amount != 0)
            {
                // Get current volume level
                double currentVolumeLevel;
                try
                {
                    response = Exec(commands[AvrCommands.VolumeGet].command);
                }
                catch (AvrException e)
                {
                    Log.Error(e, "Volume get error");
                    return;
                }
                Match match = commands[AvrCommands.VolumeGet].expect.Match(response);
                if (match.Success)
                {
                    // Get current volume level 
                    currentVolumeLevel = Convert.ToDouble(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    Log.Debug("Volume: " + match.Groups[1].Value);
                }
                else
                {
                    Log.Debug("Volume: " + response);
                    return;
                }

                // Calculate new volume level
                double volumeAmount;
                if (ratio) 
                    volumeAmount = Math.Round(currentVolumeLevel * (amount / 100));
                else 
                    volumeAmount = amount * 2;

                double volumeLevel = (action == "u") ? currentVolumeLevel + volumeAmount : currentVolumeLevel - volumeAmount;
                if (volumeLevel < 0) volumeLevel = 0;
                if (volumeLevel > 185) volumeLevel = 185;

                // Set new volume level
                string command = commands[AvrCommands.VolumeSet].command.Replace("***", volumeLevel.ToString("000", CultureInfo.InvariantCulture));
                VolumeAction(AvrCommands.VolumeSet, command);
            }
            else
            {
                // Standard increase / decrease
                if (action == "u")
                    VolumeAction(AvrCommands.VolumeUp);
                else
                    VolumeAction(AvrCommands.VolumeDown);
            }
        }

        /// <summary>
        /// Update the Volume property with current volume
        /// Avr is considered connected
        /// </summary>
        /// <exception cref="AvrException">
        /// Communication error with Avr.</exception>
        public void VolumeGet() => VolumeAction(AvrCommands.VolumeGet);

        /// <summary>
        /// Power off Avr. Response is not checked for performance reasons.
        /// Avr is considered connected
        /// </summary>
        /// <exception cref="AvrException">
        /// Communication error with Avr.</exception>
        public void PowerOff() => Send(commands[AvrCommands.PowerOff].command);

        /// <summary>
        /// Power on Avr. Response is not checked for performance reasons.
        /// Avr connection is resetted as we have lost the connection when power was down.
        /// </summary>
        /// <exception cref="AvrException">
        /// Network timeout or communication error with Avr.</exception>
        public void PowerOn() => Send(commands[AvrCommands.PowerOn].command);

        /// <summary>
        /// Get power state of Avr
        /// </summary>
        /// <exception cref="AvrException">
        /// Network timeout or communication error with Avr.</exception>
        public PowerState PowerStatus()
        {
            string response;
            // Initialize with unknown state
            PowerState state = PowerState.PowerUnknown;

            try
            {
                response = Exec(commands[AvrCommands.PowerOn].command);
            }
            catch (AvrException e)
            {
                Log.Error(e, "Power status error");
                return state;
            }

            Match match = commands[AvrCommands.PowerOn].expect.Match(response);
            if (match.Success)
            {
                state = match.Groups[1].Value == "0" ? PowerState.PowerOn : PowerState.PowerOff;
                Log.Debug("Power state: " + state.ToString());
            }
            else
            {
                Log.Debug("Volume: " + response);
            }
            return state;
        }

        // Execute the given volume command
        // Just log error in case of AvrException 
        // Volume is unchanged in case of error
        // Assume that thsi function is called from api server thread
        // Can't access directly Volume property
        private void VolumeAction(AvrCommands avrCommand, string command)
        {
            string response;

            try
            {
                response = Exec(command);
            }
            catch (AvrException e)
            {
                Log.Error(e, "Volume action error");
                return;
            }

            Match match = commands[avrCommand].expect.Match(response);
            if (match.Success)
            {
                Volume = VolumeTodB(match.Groups[1].Value);
                // Show the popup for n seconds
                VolumePopupShow();
                Log.Debug("Volume: " + Volume);
            }
            else
            {
                Log.Debug("Volume: " + response);
            }
        }
        // Overload without specific command
        private void VolumeAction(AvrCommands avrCommand) => VolumeAction(avrCommand, commands[avrCommand].command);

        // Convert Volume from '000' format to '[+/-]0.0 dB' format
        private static string VolumeTodB(string volume)
        {
            double volumeLevel = Convert.ToDouble(volume, CultureInfo.InvariantCulture);

            if (volumeLevel == 0)
            {
                return "-.- dB";
            }
            else
            {
                volumeLevel = (volumeLevel - 161) / 2;
                return volumeLevel.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB";
            }
        }
    }
}
