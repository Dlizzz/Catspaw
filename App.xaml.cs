using System;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using Catspaw.Properties;
using Catspaw.Pioneer;
using Catspaw.Samsung;
using Catspaw.Api;
using System.Threading;

namespace Catspaw
{

    #region Global constants and enum
    /// <summary>
    /// Define the power states for the system components 
    /// </summary>
    public enum PowerState : int
    {
        /// <summary>The component is switched on</summary>
        PowerOn,
        /// <summary>The component is switched off</summary>
        PowerOff,
        /// <summary>The power state for the component is unknown</summary>
        PowerUnknown
    }
    #endregion

    /// <summary>
    /// Logique d'interaction pour App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private Avr pioneerAvr;
        private Tv samsungTv;
        private ApiServer apiServer;
        private Mutex CatspawInstance;

        #region Application events
        // Triggered when application starts. Do initialization.
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Check if an instance is already runnin by checking a named mutex owned by the first instance            
            CatspawInstance = new Mutex(true, "CatspawInstance", out bool IsNewInstance);
            if (!IsNewInstance)
            {
#pragma warning disable CA1303 // Ne pas passer de littéraux en paramètres localisés
                _ = MessageBox.Show("Catspaw is already running...", "Catspaw", MessageBoxButton.OK, MessageBoxImage.Information);
#pragma warning restore CA1303 // Ne pas passer de littéraux en paramètres localisés
                Shutdown();
                return;
            }

            // Initialize application logger with log file in local AppData path for non roaming user
            LogInit();

            // Create main window and add handler for closing
            MainWindow = new MainWindow();

            // Create Notify icon with handler for double click and context menu
            NotifyIconInit();

            // Add the power events handler to the system power events queue
            SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(PowerEventHandler);

            // Initialize Pioneer AVR
            try
            {
                pioneerAvr = new Avr(Settings.Default.AvrHostname, Settings.Default.AvrPort);
            }
            catch (AvrException err)
            {
                Log.Error(Catspaw.Properties.Resources.ErrorConnectionAvr, err);
                Log.Information("Oups! Sorry we can't connect to the Audio Video Reciever :-(");
            }
            if (pioneerAvr != null)
            {
                Log.Debug("Adding Pioneer Avr component");
                Log.Information("Cool! We've just added the Pioneer Audio Video Reciever as an HTPC component.");
            }

            //Initialize Samsung TV
            try
            {
                samsungTv = new Tv();
            }
            catch (CecException err)
            {
                Log.Debug(Catspaw.Properties.Resources.ErrorNoCecController, err);
                Log.Information("Oups! Sorry we can't connect the CeC bus controller :-(");
            }
            if (samsungTv != null)
            {
                Log.Debug("Adding Pioneer Tv component");
                Log.Information("Even cooler! We've just added the Samsung TV as an HTPC component.");
            }

            // Initialize Api server
            apiServer = new ApiServer();

            // Show main window for 5 seconds and hide it
            // DispatcherTimer setup
            var timer = new DispatcherTimer() {Interval = TimeSpan.FromSeconds(3)};
            timer.Tick += (object sender, EventArgs e) =>
            {
                if (!MainWindow.IsMouseOver) MainWindow.Hide();
                timer.Stop();
            };
            MainWindowShow();
            timer.Start();
        }

        // Triggered when the user ends the Windows session by logging off or shutting down the operating system.
        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e) => ExitApplication();

        // Triggered when the user exit the application
        private void Application_Exit(object sender, ExitEventArgs e) => ExitApplication();
        #endregion

        #region Application helpers
        /// <summary>
        /// Get the String log sink for binding 
        /// </summary>
        public StringSink LogText { get; private set; }

        /// <summary>
        /// Logfile of the application
        /// </summary>
        public string LogFile { get; private set; }

        // Initialize log file
        private void LogInit()
        {
            // Initialize application logger with log file in local AppData path for non roaming user
            // and String sink to bind to TextLog
            LogText = new StringSink(" - {Message:lj}{NewLine}");
            LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catspaw.log");
            // Configure logger with file, debug and bindablelog sinks
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .WriteTo.File(LogFile, outputTemplate:
                    "{Timestamp:dd/MM/yyyy@HH:mm:ss} - {Level:u3} - {Message:lj}{NewLine}{Exception}")
                .WriteTo.Sink(LogText, LogEventLevel.Information)
                .CreateLogger();

            // Report logger is started
            Log.Debug("====================");
            Log.Debug("Catspaw log started!");
            Log.Information("Hello world!");
        }

        // Initialize Notify icon with context menu
        private void NotifyIconInit()
        {
            // Create the NotifyIcon and add handler for double click
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = Catspaw.Properties.Resources.catspaw_128x128;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += (s, args) => MainWindowShow();

            // Create the context menu
            notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add(Catspaw.Properties.Resources.StrContextMenuShowWindow).Click += (s, e) => MainWindowShow();
            notifyIcon.ContextMenuStrip.Items.Add(Catspaw.Properties.Resources.StrContextMenuExit).Click += (s, e) => Shutdown();
        }

        // Show and activate the main window
        private void MainWindowShow()
        {
            if (MainWindow.IsVisible)
            {
                if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
            else
            {
                MainWindow.Show();
            }
        }

        // Exit the application and dispose resources
        private void ExitApplication()
        {
            // Set notify icon invisible to clean the system tray and dispose it
            if (notifyIcon != null) notifyIcon.Visible = false;
        }
#endregion

#region System power events
        // Manage power events to switch on or off teh components
        private void PowerEventHandler(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                // Resuming the system
                case PowerModes.Resume:
                    Log.Information("Hello again. I'm back to life!");

                    // If we have a Tv, try to power it on. Report if it fails.
                    try
                    {
                        samsungTv?.PowerOn();
                    }
                    catch (CecException err)
                    {
                        Log.Debug("Powering on TV set failed", err);
                    }
                    // If we have an Avr, try to power it on. Report if it fails.
                    try
                    {
                        pioneerAvr?.PowerOn();
                    }
                    catch (AvrException err)
                    {
                        Log.Debug("Powering on AVR failed", err);
                    }
                    break;

                // Suspending the system
                case PowerModes.Suspend:
                    Log.Information("Have a good night. I'm falling asleep... ");

                    // If we have a Tv, try to power it off. Report if it fails.
                    try
                    {
                        samsungTv?.PowerOff();
                    }
                    catch (CecException err)
                    {
                        Log.Debug("Powering off TV set failed", err);
                    }
                    // If we have an Avr, try to power it off. Report if it fails.
                    try
                    {
                        pioneerAvr?.PowerOff();
                    }
                    catch (AvrException err)
                    {
                        Log.Debug("Powering off AVR failed", err);
                    }
                    break;

                // Nothing to do
                case PowerModes.StatusChange:
                    break;
            }
        }
#endregion

#region Dispose
        private bool disposedValue;
        
        /// <summary>
        /// Dispose managed and unmanaged resources
        /// </summary>
        /// <param name="disposing">True to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    samsungTv?.Dispose();
                    pioneerAvr?.Dispose();
                    apiServer?.Dispose();
                    notifyIcon?.Dispose();
                    Log.CloseAndFlush();
                    // Because SystemEvents.PowerModeChanged Event is a static event, 
                    // you must detach your event handlers when your application is disposed, or memory leaks will result.
                    SystemEvents.PowerModeChanged -= PowerEventHandler;
                    CatspawInstance?.Dispose();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Implement dispose IDisposable interface
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

#region Native methods
    internal static class NativeMethods
    {
        /// <summary>
        /// Suspends the system by shutting power down. Depending on the Hibernate parameter, the system either enters a suspend (sleep) state or hibernation (S4).
        /// </summary>
        /// <param name="hibernate">If this parameter is TRUE, the system hibernates. If the parameter is FALSE, the system is suspended.</param>
        /// <param name="forceCritical">Windows Server 2003, Windows XP, and Windows 2000:  If this parameter is TRUE,
        /// the system suspends operation immediately; if it is FALSE, the system broadcasts a PBT_APMQUERYSUSPEND event to each
        /// application to request permission to suspend operation.</param>
        /// <param name="disableWakeEvent">If this parameter is TRUE, the system disables all wake events. If the parameter is FALSE, any system wake events remain enabled.</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        /// <remarks>See http://msdn.microsoft.com/en-us/library/aa373201(VS.85).aspx</remarks>
        [DllImport("Powrprof.dll", SetLastError = true)]
        internal static extern uint SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    }
#endregion
}
