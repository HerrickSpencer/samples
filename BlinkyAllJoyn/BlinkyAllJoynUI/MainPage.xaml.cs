using System;
using System.Collections.Generic;
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
using com.microsoft.maker.Blinky;
using Windows.Devices.AllJoyn;
using Windows.UI.Popups;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BlinkyAllJoynUI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer blinkyTimer;
        private Windows.ApplicationModel.Resources.ResourceLoader loader;
        private int LEDStatus = 0;
        private readonly int LED_PIN = 5; // on-board LED on the Rpi2
        private GpioPin pin;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        AllJoynBusAttachment allJoynBusAttachment;
        BlinkyWatcher blinkyWatcher;
        BlinkyDevice blinkyDevice;
        private bool sliderSliding = false;

        public MainPage()
        {
            loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            this.InitializeComponent();

            InitializeGPIO();
            InitializeAlljoyn();

            blinkyTimer = new DispatcherTimer();
            blinkyTimer.Interval = TimeSpan.FromMilliseconds(0);
            blinkyTimer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, object e)
        {
            if (this.blinkyDevice.IsBlinking)
            {
                FlipLED();
            }
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
        #region Alljoyn
        private void InitializeAlljoyn()
        {
            allJoynBusAttachment = new AllJoynBusAttachment();
            blinkyWatcher = new BlinkyWatcher(allJoynBusAttachment);
            blinkyWatcher.Added += BlinkyWatcher_Added;
            blinkyWatcher.Stopped += BlinkyWatcher_Stopped;
            blinkyWatcher.Start();
        }

        private void BlinkyWatcher_Stopped(BlinkyWatcher sender, AllJoynProducerStoppedEventArgs args)
        {
            this.BlinkyAllJoynStatus.Text = "Blinky removed!";
        }

        private async void BlinkyWatcher_Added(BlinkyWatcher sender, AllJoynServiceInfo args)
        {
            BlinkyJoinSessionResult joinSessionResult = await BlinkyConsumer.JoinSessionAsync(args, sender);
            this.BlinkyAllJoynStatus.Text = String.Format("Blinky added from {0}!", args.UniqueName);

            if (joinSessionResult.Status == AllJoynStatus.Ok)
            {
                this.blinkyDevice = new BlinkyDevice(joinSessionResult.Consumer);
                this.blinkyDevice.PropertyChanged += BlinkyDevice_PropertyChanged;
                UpdateBlinking();
                UpdateBlinkInterval();
            }
        }

        private void BlinkyDevice_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsBlinking":
                    UpdateBlinking();
                    break;
                case "BlinkInterval":
                    UpdateBlinkInterval();
                    break;
            }
        }

        private async void UpdateBlinkInterval()
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.blinkyTimer.Interval = TimeSpan.FromMilliseconds(this.blinkyDevice.BlinkInterval);
                if (this.Delay.Value != this.blinkyDevice.BlinkInterval)
                {
                    this.Delay.Value = this.blinkyDevice.BlinkInterval;
                }
                this.DelayText.Text = string.Format("{0}ms", this.Delay.Value);
                if (!this.blinkyTimer.IsEnabled)
                {
                    this.blinkyTimer.Start();
                }
            });
        }

        private async void UpdateBlinking()
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

                if (this.blinkyDevice.IsBlinking)
                {
                    BlinkyStartStop.Content = loader.GetString("BlinkyStop");
                }
                else
                {
                    BlinkyStartStop.Content = loader.GetString("BlinkyStart");
                }
            });
        }
        #endregion

        private void TraceMessage(string message = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Debug.WriteLine("message: " + message + this.Delay.Value.ToString() + " vs " + this.blinkyDevice.BlinkInterval.ToString());
            Debug.WriteLine("member name: " + memberName);
            Debug.WriteLine("source file path: " + sourceFilePath);
            Debug.WriteLine("source line number: " + sourceLineNumber);
        }

        private void BlinkyStartStop_Click(object sender, RoutedEventArgs e)
        {
            this.blinkyDevice.IsBlinking = !this.blinkyDevice.IsBlinking;
        }

        private void Delay_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!this.sliderSliding)
            {
                this.TraceMessage();
                this.blinkyDevice.BlinkInterval = e.NewValue;
            }
        }
        
        private void Delay_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = false;
            this.blinkyDevice.BlinkInterval = this.Delay.Value;
        }

        private void Delay_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = true;
        }

        private void Delay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = false;
        }

        private void Delay_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = false;
        }

        private void Delay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = false;
        }

        private void Delay_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            this.TraceMessage();
            this.sliderSliding = true;
        }
    }
}
