using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.AllJoyn;
using Windows.Foundation;
using com.microsoft.maker.Blinky;


namespace BlinkyAllJoyn
{
    internal class BlinkyService : IBlinkyService
    {
        private AllJoynBusAttachment allJoynBusAttachment;
        private BlinkyProducer producer;
        private bool isBlinking = true;
        private double blinkInterval = 10;

        public delegate void BlinkIntervalChangedHandler(object sender, double interval);
        public event BlinkIntervalChangedHandler BlinkIntervalChanged;

        public delegate void BlinkStateChangedHandler(object sender, bool isBlinking);
        public event BlinkStateChangedHandler BlinkStateChanged;

        internal void Initialize()
        {
            this.allJoynBusAttachment = new AllJoynBusAttachment();
            this.producer = new BlinkyProducer(this.allJoynBusAttachment);
            this.allJoynBusAttachment.AboutData.DefaultAppName = Package.Current.DisplayName;
            this.allJoynBusAttachment.AboutData.DefaultDescription = Package.Current.Description;
            this.allJoynBusAttachment.AboutData.DefaultManufacturer = Package.Current.Id.Publisher;
            this.allJoynBusAttachment.AboutData.SoftwareVersion = Package.Current.Id.Version.ToString();
            this.allJoynBusAttachment.AboutData.IsEnabled = true;
            this.producer.Service = this;
            this.producer.Start();
        }

        public IAsyncOperation<BlinkyStartResult> StartAsync(AllJoynMessageInfo info)
        {
            return Task.Run(() =>
            {
                this.isBlinking = true;
                this.BlinkStateChanged(this, this.isBlinking);
                this.producer.EmitIsBlinkingChanged();
                return new BlinkyStartResult();
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BlinkyStopResult> StopAsync(AllJoynMessageInfo info)
        {
            return Task.Run(() =>
            {
                this.isBlinking = false;
                this.BlinkStateChanged(this, this.isBlinking);
                this.producer.EmitIsBlinkingChanged();
                return new BlinkyStopResult();
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BlinkyGetVersionResult> GetVersionAsync(AllJoynMessageInfo info)
        {
            return Task.Run(() =>
            {
                return BlinkyGetVersionResult.CreateSuccessResult(Package.Current.Id.Version.Major);
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BlinkyGetBlinkIntervalResult> GetBlinkIntervalAsync(AllJoynMessageInfo info)
        {
            return Task.Run(() =>
            {
                return BlinkyGetBlinkIntervalResult.CreateSuccessResult(this.blinkInterval);
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BlinkySetBlinkIntervalResult> SetBlinkIntervalAsync(AllJoynMessageInfo info, double value)
        {
            return Task.Run(() =>
            {
                this.blinkInterval = value;
                this.BlinkIntervalChanged(this, this.blinkInterval);
                this.producer.EmitBlinkIntervalChanged();
                return BlinkySetBlinkIntervalResult.CreateSuccessResult();
            }).AsAsyncOperation();
        }

        public IAsyncOperation<BlinkyGetIsBlinkingResult> GetIsBlinkingAsync(AllJoynMessageInfo info)
        {
            return Task.Run(() =>
            {
                return BlinkyGetIsBlinkingResult.CreateSuccessResult(this.isBlinking);
            }).AsAsyncOperation();
        }
    }
}
