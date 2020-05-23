using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Catspaw.Pioneer
{
    public partial class Avr
    {
        private const RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.Singleline;

        private enum AvrCommands 
        { 
            PowerState,
            PowerOn,
            PowerOff
        }

        // Initialize the actions dictionnary for the avr
        private static readonly Dictionary<AvrCommands, (string command, Regex expect)> commands = new Dictionary<AvrCommands, (string command, Regex expect)>
        {
            { AvrCommands.PowerState, ("?P", new Regex("PWR([0-1])", regexOptions)) },
            { AvrCommands.PowerOn,    ("PO", new Regex("PWR0", regexOptions)) },
            { AvrCommands.PowerOff,   ("PF", new Regex("PWR1", regexOptions)) }
        };

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
            PowerState state = PowerState.PowerUnknown;

            string response = Exec(commands[AvrCommands.PowerOn].command);
            
            Match match = commands[AvrCommands.PowerOn].expect.Match(response);
            if (match.Success)
                state = match.Groups[1].Value == "0" ? PowerState.PowerOn : PowerState.PowerOff;

            return state;
        }
    }
}
