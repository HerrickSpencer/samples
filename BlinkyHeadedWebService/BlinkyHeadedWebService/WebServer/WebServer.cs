using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace BlinkyHeadedWebService
{
    //public class WebServer
    //{
    //    public event HttpServer.BlinkIntervalChangedHandler BlinkIntervalChanged;

    //    /// <summary>
    //    /// Starts the web server on the specified port
    //    /// </summary>
    //    /// <param name="serverPort">Web server port</param>
    //    public void Start(int serverPort)
    //    {
    //        var server = new HttpServer(serverPort);
    //        server.BlinkIntervalChanged += Server_BlinkIntervalChanged;
    //        IAsyncAction asyncAction = ThreadPool.RunAsync(
    //            (s) =>
    //            {
    //                server.StartServer();
    //            });
    //    }

    //    private void Server_BlinkIntervalChanged(int newBlinkInterval)
    //    {
    //        if (this.BlinkIntervalChanged!=null)
    //        {
    //            this.BlinkIntervalChanged(newBlinkInterval);
    //        }
    //    }
    //}

    /// <summary>
    /// HttpServer class that services the content for the web interface
    /// </summary>
    public sealed class HttpServer : IDisposable
    {
        private const uint BufferSize = 8192;
        private int port = 8000;
        private readonly StreamSocketListener listener;
        private WebHelper helper;
        private int blinkInterval;

        public int BlinkInterval
        {
            get
            {
                return blinkInterval;
            }

            set
            {
                if (blinkInterval != value && value >=0 && value <= 100)
                { 
                    blinkInterval = value;
                    RaiseBlinkIntervalChanged();
                }
            }
        }

        public delegate void BlinkIntervalChangedHandler(int newBlinkInterval);
        public event BlinkIntervalChangedHandler BlinkIntervalChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverPort">Port to start server on</param>
        public HttpServer(int serverPort)
        {
            helper = new WebHelper();
            listener = new StreamSocketListener();
            port = serverPort;
            listener.ConnectionReceived += (s, e) =>
            {
                try
                {
                    // Process incoming request
                    processRequestAsync(e.Socket);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception in StreamSocketListener.ConnectionReceived(): " + ex.Message);
                }
            };
        }

        public async void StartServer()
        {
            await helper.InitializeAsync();

#pragma warning disable CS4014
            listener.BindServiceNameAsync(port.ToString());
#pragma warning restore CS4014

            this.RaiseBlinkIntervalChanged(); //init a value;
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        /// <summary>
        /// Process the incoming request
        /// </summary>
        /// <param name="socket"></param>
        private async void processRequestAsync(StreamSocket socket)
        {
            try
            {
                StringBuilder request = new StringBuilder();
                using (IInputStream input = socket.InputStream)
                {
                    // Convert the request bytes to a string that we understand
                    byte[] data = new byte[BufferSize];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = BufferSize;
                    while (dataRead == BufferSize)
                    {
                        await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                using (IOutputStream output = socket.OutputStream)
                {
                    // Parse the request
                    string[] requestParts = request.ToString().Split('\n');
                    string requestMethod = requestParts[0];
                    string[] requestMethodParts = requestMethod.Split(' ');

                    // Process the request and write a response to send back to the browser
                    if (requestMethodParts[0].ToUpper() == "GET")
                    {
                        Debug.WriteLine("request for: {0}", requestMethodParts[1]);
                        await writeResponseAsync(requestMethodParts[1], output, socket.Information);
                    }
                    if (requestMethodParts[0].ToUpper() == "POST")
                    {
                        string requestUri = string.Format("{0}?{1}", requestMethodParts[1], requestParts[14]);
                        Debug.WriteLine("POST request for: {0} ", requestUri);
                        await writeResponseAsync(requestUri, output, socket.Information);
                    }
                    else
                    {
                        throw new InvalidDataException("HTTP method not supported: "
                                                       + requestMethodParts[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in processRequestAsync(): " + ex.Message);
            }
        }

        private async Task writeResponseAsync(string request, IOutputStream os, StreamSocketInformation socketInfo)
        {
            try
            {
                request = request.TrimEnd('\0'); //remove possible null from POST request

                string[] requestParts = request.Split('/');
                // Request for the root page, so redirect to home page
                if (request.Equals("/"))
                {
                    await redirectToPage(NavConstants.HOME_PAGE, os);
                }
                // Request for the home page
                else if (request.Contains(NavConstants.HOME_PAGE))
                {
                    // Generate the default config page
                    string html = await helper.GenerateStatusPage();
                    await WebHelper.WriteToStream(html, os);
                }
                else if (request.Contains(NavConstants.BLINKY_SETINTERVAL))
                {
                    if (!string.IsNullOrEmpty(request))
                    {
                        Dictionary<string, string> parameters = WebHelper.ParseGetParametersFromUrl(new Uri(string.Format("http://0.0.0.0/{0}", request)));

                        if (parameters.ContainsKey("blinkyIntervalSlide"))
                        {
                            int newInterval = BlinkInterval;
                            if (int.TryParse(parameters["blinkyIntervalSlide"], out newInterval))
                            {
                                if (newInterval >= 0 && newInterval != BlinkInterval)
                                {
                                    BlinkInterval = newInterval;
                                    RaiseBlinkIntervalChanged();
                                }
                            }
                        }
                    }
                    await redirectToPage(NavConstants.BLINKY_PAGE, os);
                }
                else if (request.Contains(NavConstants.BLINKY_PAGE))
                {
                    string html = await helper.GenerateBlinkyPage(BlinkInterval);
                    await WebHelper.WriteToStream(html, os);
                }
                // Request for a file that is in the Assets\Web folder (e.g. logo, css file)
                else
                {
                    using (Stream resp = os.AsStreamForWrite())
                    {
                        string filePath = "";
                        bool exists = true;
                        try
                        {
                            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

                            // Map the requested path to Assets\Web folder
                            filePath = @"Assets\Web" + request.Replace('/', '\\');

                            // Open the file and write it to the stream
                            using (Stream fs = await folder.OpenStreamForReadAsync(filePath))
                            {
                                string contentType = "";
                                if (request.Contains("css"))
                                {
                                    contentType = "Content-Type: text/css\r\n";
                                }
                                if (request.Contains("htm"))
                                {
                                    contentType = "Content-Type: text/html\r\n";
                                }
                                string header = String.Format("HTTP/1.1 200 OK\r\n" +
                                                "Content-Length: {0}\r\n{1}" +
                                                "Connection: close\r\n\r\n",
                                                fs.Length,
                                                contentType);
                                byte[] headerArray = Encoding.UTF8.GetBytes(header);
                                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                                await fs.CopyToAsync(resp);
                            }
                        }
                        catch (FileNotFoundException ex)
                        {
                            exists = false;

                            Debug.WriteLine("{0} : {1}", filePath, ex.Message);
                        }

                        // Send 404 not found if can't find file
                        if (!exists)
                        {
                            byte[] headerArray = Encoding.UTF8.GetBytes(
                                                  "HTTP/1.1 404 Not Found\r\n" +
                                                  "Content-Length:0\r\n" +
                                                  "Connection: close\r\n\r\n");
                            await resp.WriteAsync(headerArray, 0, headerArray.Length);
                        }

                        await resp.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in writeResponseAsync(): " + ex.Message);
                Debug.WriteLine(ex.StackTrace);

                Debug.WriteLine(ex.Message);

                try
                {
                    // Try to send an error page back if there was a problem servicing the request
                    string html = helper.GeneratePage("Error", "Error", "There's been an error: " + ex.Message + "<br><br>" + ex.StackTrace);
                    await WebHelper.WriteToStream(html, os);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        private void RaiseBlinkIntervalChanged()
        {
            if (this.BlinkIntervalChanged != null)
            {
                this.BlinkIntervalChanged(this.BlinkInterval);
            }
        }

        /// <summary>
        /// Redirect to a page
        /// </summary>
        /// <param name="path">Relative path to page</param>
        /// <param name="os"></param>
        /// <returns></returns>
        private async Task redirectToPage(string path, IOutputStream os)
        {
            using (Stream resp = os.AsStreamForWrite())
            {
                byte[] headerArray = Encoding.UTF8.GetBytes(
                                  "HTTP/1.1 302 Found\r\n" +
                                  "Content-Length:0\r\n" +
                                  "Location: /" + path + "\r\n" +
                                  "Connection: close\r\n\r\n");
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await resp.FlushAsync();
            }
        }
    }
}
