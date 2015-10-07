using com.microsoft.maker.Blinky;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.AllJoyn;

namespace BlinkyAllJoynUI
{
    public class BlinkyDevice : Observable
    {
        BlinkyConsumer blinkyConsumer;
        bool isBlinking = false;
        double blinkInterval = 0;

        public BlinkyDevice(BlinkyConsumer consumer)
        {
            this.blinkyConsumer = consumer;
            this.blinkyConsumer.BlinkIntervalChanged += BlinkyConsumer_BlinkIntervalChanged;
            this.blinkyConsumer.IsBlinkingChanged += BlinkyConsumer_IsBlinkingChanged;
            BlinkyConsumer_BlinkIntervalChanged(blinkyConsumer, null);
            BlinkyConsumer_IsBlinkingChanged(blinkyConsumer, null);
        }

        public bool IsBlinking
        {
            get
            {
                return isBlinking;
            }

            set
            {
                if (isBlinking != value)
                {
                    if (value)
                    {
                         this.Start();
                    }
                    else
                    {
                        this.Stop();
                    }
                }
            }
        }

        public double BlinkInterval
        {
            get
            {
                return blinkInterval;
            }

            set
            {
                ChangeBlinkInterval(value);
            }
        }

        private async void BlinkyConsumer_IsBlinkingChanged(BlinkyConsumer sender, object args)
        {
            BlinkyGetIsBlinkingResult result = await sender.GetIsBlinkingAsync();
            //await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            //{
                if (result.Status == AllJoynStatus.Ok)
                {
                    SetProperty(ref isBlinking, result.IsBlinking, "IsBlinking");
                }
            //});
        }

        private async void BlinkyConsumer_BlinkIntervalChanged(BlinkyConsumer sender, object args)
        {
            BlinkyGetBlinkIntervalResult result = await sender.GetBlinkIntervalAsync();
            //await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            //{
                if (result.Status == AllJoynStatus.Ok)
                {
                    SetProperty(ref blinkInterval, result.BlinkInterval, "BlinkInterval");
                }
            //});
        }

        private async void Start()
        {
            if (this.isBlinking)
            {
                return;
            }

            BlinkyStartResult result = await this.blinkyConsumer.StartAsync();
            if (result.Status == AllJoynStatus.Ok)
            {
                SetProperty(ref isBlinking, true, "IsBlinking");
            }
        }

        private async void Stop()
        {
            if (this.isBlinking)
            {
                BlinkyStopResult result = await this.blinkyConsumer.StopAsync();
                if (result.Status == AllJoynStatus.Ok)
                {
                    SetProperty(ref isBlinking, false, "IsBlinking");
                }
            }
        }

        private async void ChangeBlinkInterval(double newInterval)
        {
            BlinkySetBlinkIntervalResult result = await this.blinkyConsumer.SetBlinkIntervalAsync(newInterval);
            if (result.Status == AllJoynStatus.Ok)
            {
                SetProperty(ref blinkInterval, newInterval, "BlinkInterval");
            }
        }
    }
}
