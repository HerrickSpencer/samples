using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace BlinkyHeadedWebService
{
    public class WebHelper
    {
        private string htmlTemplate;
        private string htmlBlinkyBody;
        private string htmlBlinkyHead;

        private Dictionary<string, string> links = new Dictionary<string, string>
            {
                {"Home", "/" + NavConstants.HOME_PAGE },
                {"BlinkyPage", "/" + NavConstants.BLINKY_PAGE }
            };

        /// <summary>
        /// Initializes the WebHelper with the default.htm template
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            // Load the html page templates
            await LoadHTMLPages();
        }
        
        /// <summary>
        /// Generates the html for the navigation bar
        /// </summary>
        /// <returns></returns>
        private string createNavBar()
        {
            // Create html for the side bar navigation using the links Dictionary
            string html = "<p>Navigation</p><ul>";
            foreach (string key in links.Keys)
            {
                //if (key.Equals("OneDrive") && App.Controller.Storage.GetType() != typeof(OneDrive))
                //    continue;

                html += "<li><a href='" + links[key] + "'>" + key + "</a></li>";
            }
            html += "</ul>";
            return html;
        }

        /// <summary>
        /// Generates the html for the home page (status page)
        /// </summary>
        /// <returns></returns>
        public async Task<string> GenerateStatusPage()
        {
            string html = "<table>";
            // Device Name
            html += "<tr><td><b>Device Name:</b></td><td>&nbsp;&nbsp;" + EnvironmentSettings.GetDeviceName() + "</td></tr>";

            // IP Address
            html += "<tr><td><b>IP Address:</b></td><td>&nbsp;&nbsp;" + EnvironmentSettings.GetIPAddress() + "</td></tr>";

            // App Version
            html += "<tr><td><b>App Version:</b></td><td>&nbsp;&nbsp;" + EnvironmentSettings.GetAppVersion() + "</td></tr>";

            // OS Version
            html += "<tr><td><b>OS Version:</b></td><td>&nbsp;&nbsp;" + EnvironmentSettings.GetOSVersion() + "</td></tr>";

            html += "<tr><td>&nbsp;</td></tr>";

            // Show up time
            html += "<tr><td><b>Up Time:</b></td><td>&nbsp;&nbsp;" + App.GlobalStopwatch.Elapsed.ToString() + "</td></tr>";

            html += "<tr><td>&nbsp;</td></tr>";

            await Task.Run(() => { Debug.WriteLine("nothing"); }); // make compiler happy

            return GeneratePage("Blinky", "Home", html);
        }

        public async Task<string> GenerateBlinkyPage(int blinkyInterval)
        {
            await Task.Run(() => { Debug.WriteLine("Generate Blinky with {0} interval", blinkyInterval); }); // make compiler happy

            return GeneratePage("Hello Blinky", "Blinky Page", htmlBlinkyBody, htmlBlinkyHead.Replace("var blinkInterval = 0;", string.Format( "var blinkInterval = {0};", blinkyInterval)));
        }

        public async Task LoadHTMLPages()
        {
            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            
            // Load default template
            var filePath = @"Assets\Web\default.htm";
            var file = await folder.GetFileAsync(filePath);
            htmlTemplate = await FileIO.ReadTextAsync(file);

            // Load the Blinky page template
            string pageName = Path.GetFileNameWithoutExtension(NavConstants.BLINKY_PAGE);

            filePath = string.Format("{0}{1}.{2}", NavConstants.ASSETSWEB, pageName, "body");
            file = await folder.GetFileAsync(filePath);
            this.htmlBlinkyBody = await FileIO.ReadTextAsync(file);

            // Load the html page template
            filePath = string.Format("{0}{1}.{2}", NavConstants.ASSETSWEB, pageName, "head");
            file = await folder.GetFileAsync(filePath);
            this.htmlBlinkyHead = await FileIO.ReadTextAsync(file);
        }

        /// <summary>
        /// Helper function to generate page
        /// </summary>
        /// <param name="title">Title that appears on the window</param>
        /// <param name="titleBar">Title that appears on the header bar of the page</param>
        /// <param name="content">Content for the body of the page</param>
        /// <param name="message">A status message that will appear above the content</param>
        /// <returns></returns>
        public string GeneratePage(string title, string titleBar, string content, string head = "", string message = "")
        {
            string html = htmlTemplate;
            html = html.Replace("#content#", content);
            html = html.Replace("#title#", title);
            html = html.Replace("#titleBar#", titleBar);
            html = html.Replace("#navBar#", createNavBar());
            html = html.Replace("#message#", message);
            html = html.Replace("#head#", head);

            return html;
        }

        /// <summary>
        /// Parses the GET parameters from the URL and returns the parameters and values in a Dictionary
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static Dictionary<string, string> ParseGetParametersFromUrl(Uri uri)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var decoder = new WwwFormUrlDecoder(uri.Query);
                foreach (WwwFormUrlDecoderEntry entry in decoder)
                {
                    parameters.Add(entry.Name, entry.Value);
                }
            }
            return parameters;
        }

        /// <summary>
        /// Writes html data to the stream
        /// </summary>
        /// <param name="data"></param>
        /// <param name="os"></param>
        /// <returns></returns>
        public static async Task WriteToStream(string data, IOutputStream os)
        {
            using (Stream resp = os.AsStreamForWrite())
            {
                // Look in the Data subdirectory of the app package
                byte[] bodyArray = Encoding.UTF8.GetBytes(data);
                MemoryStream stream = new MemoryStream(bodyArray);
                string header = String.Format("HTTP/1.1 200 OK\r\n" +
                                  "Content-Length: {0}\r\n" +
                                  "Connection: close\r\n\r\n",
                                  stream.Length);
                byte[] headerArray = Encoding.UTF8.GetBytes(header);
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await stream.CopyToAsync(resp);
                await resp.FlushAsync();
            }
        }

        /// <summary>
        /// Writes a file to the stream
        /// </summary>
        /// <param name="file"></param>
        /// <param name="os"></param>
        /// <returns></returns>
        public static async Task WriteFileToStream(StorageFile file, IOutputStream os)
        {
            using (Stream resp = os.AsStreamForWrite())
            {
                bool exists = true;
                try
                {
                    using (Stream fs = await file.OpenStreamForReadAsync())
                    {
                        string header = String.Format("HTTP/1.1 200 OK\r\n" +
                                        "Content-Length: {0}\r\n" +
                                        "Connection: close\r\n\r\n",
                                        fs.Length);
                        byte[] headerArray = Encoding.UTF8.GetBytes(header);
                        await resp.WriteAsync(headerArray, 0, headerArray.Length);
                        await fs.CopyToAsync(resp);
                    }
                }
                catch (FileNotFoundException ex)
                {
                    exists = false;

                    Debug.WriteLine(ex.Message );
                }

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

        /// <summary>
        /// Makes a html hyperlink
        /// </summary>
        /// <param name="text">Hyperlink text</param>
        /// <param name="url">Hyperlink URL</param>
        /// <param name="newWindow">Should the link open in a new window</param>
        /// <returns></returns>
        public static string MakeHyperlink(string text, string url, bool newWindow)
        {
            return "<a href='" + url + "' " + ((newWindow) ? "target='_blank'" : "") + ">" + text + "</a>";
        }        
    }
}
