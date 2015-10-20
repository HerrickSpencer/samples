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
        private Windows.ApplicationModel.Resources.ResourceLoader loader;
        private int LEDStatus = 0;
        private readonly int LED_PIN = 47; // on-board LED on the Rpi2
        private GpioPin pin;

        private static Uri localPage = new Uri(@"http://localhost:8000");

        public MainPage()
        {
            loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            this.InitializeComponent();

            InitializeGPIO();

            webServer = new HttpServer(8000);
            webServer.BlinkIntervalChanged += WebServer_BlinkIntervalChanged;
            webServer.StartServer();

            //this.webView.Source = localPage;
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
                this.webView.Source = localPage;
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
                    //statusText.Text = loader.GetString("ProblemsInitializingGPIOPin");
                    return;
                }

                pin.Write(GpioPinValue.High);
                pin.SetDriveMode(GpioPinDriveMode.Output);
                //statusText.Text = loader.GetString("GPIOPinInitializedCorrectly");
            }
            catch (Exception)
            {
                // Show an error if there is no GPIO controller
                pin = null;
                //statusText.Text = loader.GetString("NoGPIOController");
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
                //LED.Fill = redBrush;
            }
            else
            {
                LEDStatus = 0;
                if (pin != null)
                {
                    pin.Write(GpioPinValue.Low);
                }
                //LED.Fill = grayBrush;
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

        private void TraceMessage(string message = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            //Debug.WriteLine("message: " + message + this.Delay.Value.ToString() + " vs " + this.blinkyTimer.Interval.ToString());
            Debug.WriteLine("member name: " + memberName);
            Debug.WriteLine("source file path: " + sourceFilePath);
            Debug.WriteLine("source line number: " + sourceLineNumber);
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateBlinkInterval(-1); //just update
        }
    }
}
