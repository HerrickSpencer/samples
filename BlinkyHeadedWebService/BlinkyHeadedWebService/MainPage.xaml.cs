using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using System.Runtime.CompilerServices;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BlinkyHeadedWebService
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        HttpServer webServer;
        private DispatcherTimer blinkyTimer;
        private Windows.ApplicationModel.Resources.ResourceLoader loader;
        private int LEDStatus = 0;
        private readonly int LED_PIN = 5; // on-board LED on the Rpi2
        private GpioPin pin;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        private bool ignoreIntervalChange = false;


        public MainPage()
        {
            loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            this.InitializeComponent();

            InitializeGPIO();

            blinkyTimer = new DispatcherTimer();
            blinkyTimer.Interval = TimeSpan.FromMilliseconds(0);
            blinkyTimer.Tick += Timer_Tick;

            webServer = new HttpServer(8000);
            webServer.BlinkIntervalChanged += WebServer_BlinkIntervalChanged;
            webServer.StartServer();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (this.blinkyTimer.IsEnabled)
            {
                FlipLED();
            }
        }

        private void WebServer_BlinkIntervalChanged(int newBlinkInterval)
        {
            Debug.WriteLine("new interval: {0}", newBlinkInterval);
            UpdateBlinkInterval(newBlinkInterval);
        }

        private async void UpdateBlinkInterval(int newBlinkInterval)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (newBlinkInterval > 0)
                {
                    this.blinkyTimer.Interval = TimeSpan.FromMilliseconds( 10000 / newBlinkInterval);
                    BlinkyStartStop.Content = loader.GetString("BlinkyStop");
                    if (!this.blinkyTimer.IsEnabled)
                    {
                        this.blinkyTimer.Start();
                    }
                }
                else
                {
                    this.blinkyTimer.Stop();
                    this.blinkyTimer.Interval = TimeSpan.FromMilliseconds(0);
                    BlinkyStartStop.Content = loader.GetString("BlinkyStart");
                }
                if (this.Delay.Value != newBlinkInterval)
                {
                    this.ignoreIntervalChange = true;
                    this.Delay.Value = newBlinkInterval;
                    this.ignoreIntervalChange = false;
                }
                this.DelayText.Text = string.Format("{0}ms", this.blinkyTimer.Interval.TotalMilliseconds);
            });
        }

        #region GPIO
        private void InitializeGPIO()
        {
            GpioController gpio = null;
            try
            {
                gpio = GpioController.GetDefault();
                pin = gpio.OpenPin(LED_PIN);

                // Show an error if the pin wasn't initialized properly
                if (pin == null)
                {
                    GpioStatus.Text = loader.GetString("ProblemsInitializingGPIOPin");
                    return;
                }

                pin.Write(GpioPinValue.High);
                pin.SetDriveMode(GpioPinDriveMode.Output);
                GpioStatus.Text = loader.GetString("GPIOPinInitializedCorrectly");
            }
            catch (Exception)
            {
                // Show an error if there is no GPIO controller
                pin = null;
                GpioStatus.Text = loader.GetString("NoGPIOController");
            }
        }

        private void FlipLED()
        {
            if (LEDStatus == 0)
            {
                LEDStatus = 1;
                if (pin != null)
                {
                    pin.Write(GpioPinValue.High);
                }
                LED.Fill = redBrush;
            }
            else
            {
                LEDStatus = 0;
                if (pin != null)
                {
                    pin.Write(GpioPinValue.Low);
                }
                LED.Fill = grayBrush;
            }
        }

        private void TurnOffLED()
        {
            if (LEDStatus == 1)
            {
                FlipLED();
            }
        }
        #endregion

        #region UIEvents
        private void TraceMessage(string message = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Debug.WriteLine("message: " + message + this.Delay.Value.ToString() + " vs " + this.blinkyTimer.Interval.ToString());
            Debug.WriteLine("member name: " + memberName);
            Debug.WriteLine("source file path: " + sourceFilePath);
            Debug.WriteLine("source line number: " + sourceLineNumber);
        }

        private void BlinkyStartStop_Click(object sender, RoutedEventArgs e)
        {
            this.webServer.BlinkInterval = 0;
        }

        private void Delay_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!this.ignoreIntervalChange)
            {
                this.TraceMessage();
                this.webServer.BlinkInterval = (int)e.NewValue;
            }
        }

        private void Delay_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = false;
            this.webServer.BlinkInterval = (int)this.Delay.Value;
        }

        private void Delay_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = true;
        }

        private void Delay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = false;
        }

        private void Delay_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = false;
        }

        private void Delay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = false;
        }

        private void Delay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            this.TraceMessage();
            this.ignoreIntervalChange = true;
        }
        #endregion
    }
}
