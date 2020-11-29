using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using Catspaw.Properties;

namespace Catspaw.Pioneer
{
    /// <summary>
    /// Implement the Avr as a Dependency object. Volume is a read-only dependency property
    /// </summary>
    public partial class Avr : DependencyObject, IDisposable
    {
        // Define the class for avr status json deserialization
#pragma warning disable CA1812
        private class AvrStatus
        {
            // Unknown
            public int S { get; set; }
            // Unknown
            public int B { get; set; }
            // Zones
            [JsonPropertyName("Z")]
            public AvrZoneStatus[] Zones { get; set; }
            // Unknown
            public int L { get; set; }
            // Unknown
            public int A { get; set; }
            // Inputs list
            [JsonPropertyName("IL")]
            public string[] Inputs { get; set; }
            // Unknown
            public string LC { get; set; }
            // Unknown
            public int MA { get; set; }
            // Unknown
            public string MS { get; set; }
            // Unknown
            public int MC { get; set; }
            // Unknown
            public int HP { get; set; }
            // Unknown
            public int HM { get; set; }
            // Unknown
            public int[] DM { get; set; }
            // Unknown
            public int H { get; set; }
        }
        private class AvrZoneStatus
        {
            // 0 is Off, 1 is On
            [JsonPropertyName("P")]
            public int Power { get; set; }
            // 0 to 185 from -80 dB to +12 dB by 0.5 dB
            [JsonPropertyName("V")]
            public int VolumeLevel { get; set; }
            // Mute: 0 is Off, 1 is On
            [JsonPropertyName("M")]
            public int Mute { get; set; }
            // Unknown
            public int[] I { get; set; }
            // Unknown
            public int C { get; set; }
        }
#pragma warning restore CA1812

        // Store the status of the avr, updated by UpdateStatus
        private AvrStatus avrStatus;

        /// <summary>
        /// Create an Avr with the given hostanme
        /// </summary>
        /// <param name="hostname">The Avr hostname</param>
        /// <exception cref="ArgumentNullException">hostname can't be null</exception>
        public Avr(string hostname)
        {
            if (hostname is null) throw new ArgumentNullException(nameof(hostname));

            // Initialize manual reset networkAvailable event to track network availability status
            // The event is created "signaled" if the network is up at creation time
            // Hook the callback to network availability changed event
            // The callback set or reset networkAvailable event depending of network status
            networkUp = new ManualResetEvent(NetworkInterface.GetIsNetworkAvailable());
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangeCallback;

            // Hostname and port for the HttpClient
            Hostname = hostname;

            // Initialize cache control on Httpclient
            avr.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
            
            // Initialize uris
            uriStatus = new UriBuilder("http", hostname, -1, uriStatusPath);
        }

        /// <summary>Get the hostname of the AVR</summary>
        public string Hostname { get; }

        #region Network availability
        // Manually reset event to track network availability status
        private readonly ManualResetEvent networkUp;

        // Signal or unsignal the network availability event when the network availability change
        private void NetworkAvailabilityChangeCallback(object sender, NetworkAvailabilityEventArgs e)
        {
            Log.Debug("Network availability changed: " + e.IsAvailable.ToString());
            if (e.IsAvailable) networkUp.Set();
            else networkUp.Reset();
        }
        #endregion

        #region Volume Popup
        // Volume popup with 2 brushes 
        private VolumePopup volumePopup;
        private static readonly Brush redBrush = new SolidColorBrush(Color.FromArgb(255, 236, 9, 40));
        private static readonly Brush greenBrush = new SolidColorBrush(Color.FromArgb(255, 9, 236, 40));

        // 2 seconds timer for volume popup hiding
        private readonly DispatcherTimer volumePopupHideTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(2000) };
        // 1.5 seconds timer for volume popup showing
        private readonly DispatcherTimer volumePopupShowTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1500) };

        /// <summary>
        /// Initialize Volume popup after Avr instance creation to allow Volume binding
        /// </summary>
        public async void InitVolume()
        {
            // Initialize volume popup with current volume
            try
            {
                await UpdateStatusAsync().ConfigureAwait(true);
            }
            catch (AvrException err)
            {
                Log.Error("Init Volume failed", err);
            }
            volumePopup = new VolumePopup();
            volumePopupShowTimer.Tick += new EventHandler(async (object sender, EventArgs e) => {
                // Stop hiding and showing timer
                volumePopupShowTimer.Stop();
                volumePopupHideTimer.Stop();
                // Update status to get current volume status
                await UpdateStatusAsync().ConfigureAwait(true);
                Log.Debug("Volume: " + Volume);
                // Label in Red if muted
                volumePopup.LblVolume.Foreground = (avrStatus.Zones[0].Mute == 1) ? redBrush : greenBrush;
                // Show the popup
                volumePopup.IsOpen = true;
                // Start hiding timer after n seconds
                volumePopupHideTimer.Start();
            });
            volumePopupHideTimer.Tick += new EventHandler((object sender, EventArgs e) => {
                // Stop hiding timer
                volumePopupHideTimer.Stop();
                // Hide the popup
                volumePopup.IsOpen = false;
            });
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
            ), DispatcherPriority.Send);
            private set => Application.Current.Dispatcher.Invoke(new Action(() => 
                SetValue(VolumePropertyKey, value)
            ), DispatcherPriority.Send);
        }

        // Convert Volume from int to '[+/-]0.0 dB' format
        private static string VolumeTodB(double volumeLevel) => 
            (volumeLevel == 0) ? 
            "-.- dB" : 
            ((volumeLevel - 161) / 2).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " dB";
        #endregion

        #region Avr communication
        // Deserialization options
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
        };

        // Avr as an HttpClient with a 1 second timeout
        private static readonly HttpClient avr = new HttpClient()
        {
            Timeout = TimeSpan.FromMilliseconds(1000),
        };
        private const string uriStatusPath = "StatusHandler.asp";
        private const string uriCommandPath = "EventHandler.asp?WebToHostItem=";
        private readonly UriBuilder uriStatus;

        // Send a command to avr. Never get an answer. Need to check avr status after.
        private async Task SendAsync(string command)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));

            // Wait for network availability event 
            // and raise exception if not signaled after default timeout
            Log.Debug("Send: wait for network");
            if (!networkUp.WaitOne(Settings.Default.AvrNetworkTimeout))
                throw new AvrException(Resources.ErrorNetworkTimeOutAvr);
            Log.Debug("Send: network ready");

            // Build Uri
            UriBuilder uriCommand = new UriBuilder("http", Hostname, -1, uriCommandPath + command);

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                Log.Debug("Send: " + uriCommand.ToString());
                HttpResponseMessage response = await avr.GetAsync(uriCommand.Uri).ConfigureAwait(true);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Log.Error(e, "Avr: Http get error");
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
        }

        // Update status of avr
        private async Task UpdateStatusAsync()
        {
            string response;
            
            // Wait for network availability event 
            // and raise exception if not signaled after default timeout
            Log.Debug("Update status: wait for network");
            if (!networkUp.WaitOne(Settings.Default.AvrNetworkTimeout))
                throw new AvrException(Resources.ErrorNetworkTimeOutAvr);
            Log.Debug("Update status: network ready");

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                Log.Debug("Update status: " + uriStatus.ToString());
                response = await avr.GetStringAsync(uriStatus.Uri).ConfigureAwait(true);
                Log.Debug("Update status: " + response);
            }
            catch (HttpRequestException e)
            {
                Log.Error(e, "Avr: Http get error");
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }

            // Deserialize json response in avr status
            avrStatus = JsonSerializer.Deserialize<AvrStatus>(response, jsonOptions);

            // Convert volume level to dB
            Volume = (avrStatus.Zones[0].Mute == 1) ? VolumeTodB(0) : VolumeTodB((double)(avrStatus?.Zones[0].VolumeLevel));
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
