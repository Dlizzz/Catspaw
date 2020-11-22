using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text;
using Catspaw.Properties;
using Serilog;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Threading;

namespace Catspaw.Pioneer
{
    /// <summary>
    /// Implement the Avr as a Dependency object. Volume is a read-only dependency property
    /// </summary>
    public partial class Avr : DependencyObject, IDisposable
    {
        // Volume popup
        private VolumePopup volumePopup;
        // 2 seconds timer for volume popup
        private readonly DispatcherTimer volumePopupTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(2) };

        // Manually reset event to track avr availability status
        private readonly ManualResetEvent networkUp;
        // Avr Ip host 
        private TcpClient avr;
        private NetworkStream avrStream;
        private StreamWriter avrWriter;
        private StreamReader avrReader;

        // Connection management
        private CancellationTokenSource tokenSource;
        private Task connection;

        /// <summary>
        /// Create an Avr with the given hostanme and TCP port
        /// </summary>
        /// <param name="hostname">The Avr hostname</param>
        /// <param name="port">The Avr tcp port (default: 23)</param>
        /// <exception cref="ArgumentNullException">hostname can't be null</exception>
        /// <exception cref="ArgumentOutOfRangeException">port must be a valid port number</exception>
        public Avr(string hostname, int port = 23)
        {
            if (hostname is null) throw new ArgumentNullException(nameof(hostname));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));

            // Initialize manual reset networkAvailable event to track network availability status
            // The event is created "signaled" if the network is up at creation time
            // Hook the callback to network availability changed event
            // The callback set or reset networkAvailable event depending of network status
            networkUp = new ManualResetEvent(NetworkInterface.GetIsNetworkAvailable());
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangeCallback;

            // Hostname and port for the TcpClient
            Hostname = hostname;
            Port = port;

            // Create the cancellation token source for the connection
            tokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Initialize Volume popup after Avr instance creation to allow Volume binding
        /// </summary>
        public void InitVolume()
        {
            // Initialize volume popup with current volume
            VolumeGet();
            volumePopup = new VolumePopup();
            volumePopupTimer.Tick += new EventHandler(VolumePopupTimerCallback);
        }

        // The read-only dependency property key associated to Volume
        private static readonly DependencyPropertyKey VolumePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(Volume),
                typeof(string), typeof(Avr),
                new FrameworkPropertyMetadata("-.- dB"));

        /// <summary>
        /// The read-only dependency property associated to Volume
        /// </summary>
        public static readonly DependencyProperty VolumeProperty = VolumePropertyKey.DependencyProperty;

        /// <summary>
        /// Set or get the text of the Sink. The given text is always append to the end of the sink.
        /// Always goes through application dispatcher to get / set the property key as it can be set by 
        /// other thread thaun UI thread
        /// </summary>
        public string Volume
        {
            get => Application.Current.Dispatcher.Invoke(new Func<string>(() => 
                (string)GetValue(VolumeProperty)
            ));
            private set => Application.Current.Dispatcher.Invoke(new Action(() => {
                SetValue(VolumePropertyKey, value);
            }));
        }

        /// <summary>Get the hostname of the AVR</summary>
        public string Hostname { get; }
        /// <summary>Get the TCP port of the AVR</summary>
        public int Port { get; }

        #region Avr network communication
        // Send a command to avr without processing avr answer
        private void Send(string command)
        {
            try
            {
                Connect();
                Log.Debug("Send command: " + command);
                avrWriter.WriteLine(command);
            }
            catch (AggregateException e)
            {
                Log.Debug("Send exception", e);
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
            finally
            {
                Disconnect();
            }
        }

        // Execute command on avr and get its response 
        private string Exec(string command)
        {
            string response;

            try
            {
                Connect();
                Log.Debug("Execute command: " + command);
                avrWriter.WriteLine(command);
                response = avrReader.ReadLine();
                // Get rid of FL response. Data are on second line
                if (response.Substring(0,2) == "FL")
                    response = avrReader.ReadLine();
            }
            catch (AggregateException e)
            {
                Log.Debug("Execute exception", e);
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
            finally
            {
                Disconnect();
            }

            return response;
        }

        // Connect to avr through TcpClient and create writer and reader
        private void Connect()
        {
            var Id = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            Log.Debug("Create TcpClient " + Id);
            // Avr is a TcpClient
            avr = new TcpClient(AddressFamily.InterNetwork);

            try
            {
                Log.Debug("Wait for network " + Id);
                // Wait for network availability event 
                // and raise exception if not signaled after default timeout
                if (!networkUp.WaitOne(Settings.Default.AvrNetworkTimeout))
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);

                Log.Debug("Wait for connection " + Id);
                // Wait for connection to Avr 
                // and raise exception if not connected after default timeout
                var token = tokenSource.Token;
                connection = Task.Run(() =>
                {
                    while(!token.IsCancellationRequested && !avr.Connected)
                    {
                        try
                        {
                            Log.Debug("Connecting to AVR... " + Id);
                            avr.Connect(Hostname, Port);
                            Log.Debug("Connected! " + Id);
                        }
                        catch (SocketException e)
                        {
                            Log.Debug("AVR socket exception during connection " + Id, e);
                            // Sleep before retrying to connect
                            Thread.Sleep(500);
                        }
                    }
                }, token);
                if (!connection.Wait(Settings.Default.AvrNetworkTimeout))
                {
                    Log.Debug("Connection wait timeout " + Id);
                    tokenSource.Cancel();
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);
                }

                Log.Debug("Create stream, reader & writer " + Id);
                // Get stream, writer, reader, send command and get response
                avrStream = avr.GetStream();
                avrWriter = new StreamWriter(avrStream, Encoding.ASCII, bufferSize: avr.SendBufferSize) { AutoFlush = true };
                avrReader = new StreamReader(avrStream, Encoding.ASCII, false, avr.ReceiveBufferSize);
            }
            catch (AggregateException e)
            {
                Log.Debug("Connect exception " + Id, e);
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
        }

        // Close and dispose connection, stream, writer and reader  
        private void Disconnect()
        {
            var Id = Thread.CurrentThread.ManagedThreadId.ToString("G", CultureInfo.InvariantCulture);

            Log.Debug("Disconnect avr " + Id);
            avrReader?.Close();
            avrWriter?.Close();
            avrStream?.Close();
            avr?.Close();
        }

        // Signal or unsignal the network availability event when the network availability change
        private void NetworkAvailabilityChangeCallback(object sender, NetworkAvailabilityEventArgs e)
        {
            Log.Debug("Network availability changed: " + e.IsAvailable.ToString());
            if (e.IsAvailable) networkUp.Set();
            else networkUp.Reset();
        }

        // Show and activate the volume popup
        // Assume that the function is called from the api server thread
        // Must go through Application dispatcher to access UI thread
        // Timer is also executed by the application dispatcher
        private void VolumePopupShow()
        {
            if (volumePopup is null) return;

            Application.Current.Dispatcher.Invoke(new Action(() => {
                // Stop existing timer if running to restart timer count
                volumePopupTimer.Stop();
                // Show popup
                volumePopup.IsOpen = true;
                // Start timer
                volumePopupTimer.Start();
            }));
        }

        // The callback function called at each timler tick
        private void VolumePopupTimerCallback(object sender, EventArgs e)
        {
            volumePopup.IsOpen = false;
            volumePopupTimer.Stop();
        }
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
                    Log.Debug("Dispose avr");
                    Disconnect();
                    tokenSource.Dispose();
                    networkUp.Close();
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
