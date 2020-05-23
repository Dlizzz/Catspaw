using System;
using CecSharp;
using Catspaw.Properties;

namespace Catspaw.Samsung
{
    /// <summary>
    /// Define the Cec bus from libcec
    /// </summary>
    public class CecBus : CecCallbackMethods
    {
        private readonly int timeout;
        private const int controllerId = 0;

        // Cec bus component
        private readonly LibCecSharp connection;
        private readonly LibCECConfiguration configuration;
        private readonly CecAdapter controller;

        /// <summary>
        /// Initialize the Cec bus without callback methods (no need) 
        /// </summary>
        /// <param name="timeout">Timeout for the connections to the bus. Must be positive</param>
        /// <exception cref="CecException">Unable to initialize Cec Bus or can't find controller</exception>
        /// <exception cref="ArgumentOutOfRangeException">timeout not strictly positive</exception>
        public CecBus(int timeout)
        {
            if (timeout <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));

            this.timeout = timeout;

            configuration = new LibCECConfiguration();
            configuration.DeviceTypes.Types[0] = CecDeviceType.PlaybackDevice;
            configuration.DeviceName = Resources.StrCecDeviceName;
            configuration.ClientVersion = LibCECConfiguration.CurrentVersion;
            configuration.SetCallbacks(this);

            connection = new LibCecSharp(configuration) ?? throw new CecException(Resources.ErrorNoCecBus);
            connection.InitVideoStandalone();

            // Get the controller on the bus
            controller = null;
            CecAdapter[] adapters = connection.FindAdapters(string.Empty);
            if (adapters.Length > 0) controller = adapters[controllerId];
            else
            {
                throw new CecException(Resources.ErrorNoCecController);
            }

            // Connection must be openned before going to suspend mode as the SCM stop the thread if we try to open
            // the connection during power event because it's an async operation
            if (!connection.Open(controller.ComPort, timeout))
                throw new CecException(Resources.ErrorNoCecControllerConnection);

            // Register active source of the connection
            connection.SetActiveSource(CecDeviceType.PlaybackDevice);
        }

        #region Callbacks
        /// <summary>Dummy RecieveCommand callback</summary>
        /// <param name="command"></param>
        /// <returns>Always 1</returns>
        public override int ReceiveCommand(CecCommand command) => 1;
        /// <summary>Dummy RecieveKeypress callback</summary>
        /// <param name="key"></param>
        /// <returns>Always 1</returns>
        public override int ReceiveKeypress(CecKeypress key) => 1;
        /// <summary>Dummy RecieveLogMessage callback</summary>
        /// <param name="message"></param>
        /// <returns>Always 1</returns>
        public override int ReceiveLogMessage(CecLogMessage message) => 1;
        #endregion

        #region Properties
        /// <summary>Get the controller path on the Cec bus</summary>
        public string Controller => controller.Path + ":" + controller.ComPort;

        /// <summary>
        /// Get the logical address of the Tv on the cec bus
        /// </summary>
        public static CecLogicalAddress TV => CecLogicalAddress.Tv;

        /// <summary>
        /// Get the current version of LibCec
        /// </summary>
        public string LibCecVersion => connection.VersionToString(configuration.ServerVersion);
        #endregion

        #region Power Management
        /// <summary>
        /// Switch power state of the given device to the requested power state
        /// </summary>
        /// <param name="device"></param>
        /// <param name="requestedPowerState"></param>
        /// <returns cref="PowerState">New PowerSate. Power state is unknown if the switch fails</returns>
        /// <exception cref="CecException">Can't connect to CecBus</exception>
        public PowerState SwitchDevicePowerState(CecLogicalAddress device, PowerState requestedPowerState)
        {
            PowerState state = PowerState.PowerUnknown;

            switch (requestedPowerState)
            {
                case PowerState.PowerOff:
                    if (IsDeviceReady(device) && connection.StandbyDevices(device)) state = PowerState.PowerOff;
                    break;
                case PowerState.PowerOn:
                    // Close and reopen the connection as we lose it when we go to sleep
                    connection.Close();
                    if (!connection.Open(controller.ComPort, timeout))
                        throw new CecException(Resources.ErrorNoCecControllerConnection);
                    // Register active source of the connection
                    connection.SetActiveSource(CecDeviceType.PlaybackDevice);

                    if (IsDeviceReady(device) && connection.PowerOnDevices(device)) state = PowerState.PowerOn;
                    break;
                default:
                    break;
            }

            return state;
        }

        /// <summary>
        /// Get power state of the given devive
        /// </summary>
        /// <param name="device"></param>
        /// <returns>The power state of the device. Power state is unknown if the query fails</returns>
        /// <exception cref="CecException">Can't connect to CecBus</exception>
        public PowerState GetDevicePowerState(CecLogicalAddress device)
        {
            PowerState state = PowerState.PowerUnknown;

            if (IsDeviceReady(device))
            {
                CecPowerStatus cecStatus = connection.GetDevicePowerStatus(device);

                state = cecStatus switch
                {
                    CecPowerStatus.On => PowerState.PowerOn,
                    CecPowerStatus.Standby => PowerState.PowerOff,
                    _ => PowerState.PowerUnknown
                };
            }

            return state;
        }

        // Return True if device is ready to recieve command
        private bool IsDeviceReady(CecLogicalAddress device) =>
            (connection.IsActiveDevice(device) && connection.PollDevice(device));
        #endregion

        #region Dispose support
        // Flag: Has Dispose already been called?
        private bool disposed = false;

        /// <summary>
        /// Close and dispose the Cec bus connection. Override the base class Dispose method
        /// </summary>
        /// <param name="disposing">True to dispose</param>
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                connection.Close();
                if (connection != null) connection.Dispose();
            }
            disposed = true;
            // Call base class implementation.
            base.Dispose(disposing);
        }
        #endregion
    }
}
