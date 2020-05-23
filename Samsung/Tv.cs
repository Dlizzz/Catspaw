using System;
using Catspaw.Properties;

namespace Catspaw.Samsung
{
    /// <summary>
    /// Define a Samsumg Tv connected to the Cec Bus and managed by the Cec controller. 
    /// </summary>
    public class Tv: IDisposable
    {
        /// <summary>
        /// Initialize the Tv by connecting to the Cec Bus and getting the bus controller
        /// </summary>
        /// <exception cref="CecException">Unable to connect to Cec bus or Cec controller not found 
        /// </exception>
        public Tv()
        {
            // Try to connect to the Cec bus
            Bus = new CecBus(Settings.Default.CecAdapterTimeout);
        }

        /// <summary>
        /// Get the Cec bus on which the TV is connected
        /// </summary>
        public CecBus Bus { get;  }

        /// <summary>
        /// Get the controller of the Cec bus on which the TV is connected
        /// </summary>
        public string Controller => Bus.Controller;

        #region Power Management
        /// <summary>
        /// Get the power state of the Tv on the Cec bus
        /// </summary>
        /// <returns cref="PowerState">New PowerState. Power state is unknown or null if it fails</returns>
        /// <exception cref="CecException">Unable to connect to Cec bus or Cec controller not found</exception>
        public PowerState? PowerStatus => Bus?.GetDevicePowerState(CecBus.TV);

        /// <summary>
        /// Switch on TV
        /// </summary>
        /// <returns cref="PowerState">New PowerState. Power state is unknown or null if it fails</returns>
        /// <exception cref="CecException">Unable to connect to Cec bus or Cec controller not found</exception>
        public PowerState? PowerOn() => Bus?.SwitchDevicePowerState(CecBus.TV, PowerState.PowerOn);

        /// <summary>
        /// Switch off TV
        /// </summary>
        /// <returns cref="PowerState">New PowerState. Power state is unknown or null if it fails</returns>
        /// <exception cref="CecException">Unable to connect to Cec bus or Cec controller not found</exception>
        public PowerState? PowerOff() => Bus?.SwitchDevicePowerState(CecBus.TV, PowerState.PowerOff);
        #endregion

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
                    Bus?.Dispose();
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
