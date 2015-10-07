using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using com.microsoft.maker.Blinky;
using Windows.Devices.AllJoyn;
using Windows.Devices.Gpio;
using System.Diagnostics;
using System.Threading;

namespace BlinkyAllJoyn
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral deferral;
        private BlinkyService blinkyService;
        private Timer timer;
        private const int LED_PIN = 5;
        private GpioPin ledPin;
        private GpioPinValue value = GpioPinValue.High;
        private bool isBlinking = true;
        private double blinkInterval = 10;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();
            InitGPIO();

            this.blinkyService = new BlinkyService();
            this.blinkyService.BlinkIntervalChanged += BlinkyService_BlinkIntervalChanged;
            this.blinkyService.BlinkStateChanged += BlinkyService_BlinkStateChanged;
            this.blinkyService.Initialize();
            this.timer = new Timer(new TimerCallback(this.Timer_Callback), null, 0, (int)this.blinkInterval); //ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(this.blinkInterval));
        }

        private void Timer_Callback(object state)
        {
            if (this.isBlinking)
            {
                value = (value == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;
                ledPin.Write(value);
            }
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                ledPin = null;
                Debug.WriteLine("There is no GPIO controller on this device.");
                return;
            }

            ledPin = gpio.OpenPin(LED_PIN);
            ledPin.Write(GpioPinValue.High);
            ledPin.SetDriveMode(GpioPinDriveMode.Output);

            Debug.WriteLine("GPIO pin initialized correctly.");
        }

        private void BlinkyService_BlinkStateChanged(object sender, bool isBlinking)
        {
            this.isBlinking = isBlinking;
        }

        private void BlinkyService_BlinkIntervalChanged(object sender, double interval)
        {
            this.blinkInterval = interval;
            this.timer.Change(0, (int)this.blinkInterval);
        }
    }
}
