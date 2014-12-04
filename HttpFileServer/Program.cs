using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace HttpFileServer
{
    internal class Program
    {
        private const string DataDir = "/Uploads";
        private const int HashSize = 8;

        private static readonly string[] Prefixes = {"http://+:80/"};
        private static HttpListener _listener;
        private static int _numGets;
        private static int _numPosts;
        private static long _bandwidth; //bytes
        private static readonly Random Random = new Random();

        private static void Main(string[] args)
        {
            Console.Title = "Kapture Server :: Gets: 0 - Posts: 0 - Bandwidth: 0MB";
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            Console.WriteLine("Mounted data directory at: " + DataDir);

            _listener = new HttpListener();
            foreach (var p in Prefixes)
                _listener.Prefixes.Add(p);

            _listener.Start();
            _listener.BeginGetContext((BeginGetCallback), null);

            Console.WriteLine("HTTP Listener started listening for " + Prefixes.Count() + " prefixes.");
            Console.ReadLine();
        }

        public static string RandomHash(int length)
        {
            const string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var result = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                result.Append(characters[Random.Next(characters.Length)]);
            }
            return result.ToString();
        }

        private static void BeginGetCallback(IAsyncResult result)
        {
            _listener.BeginGetContext(BeginGetCallback, null);
            var request = _listener.EndGetContext(result);

            switch (request.Request.HttpMethod.ToUpper())
            {
                case "POST":
                    HandlePost(request);
                    break;
                case "GET":
                    HandleGet(request);
                    break;
                default:
                    Console.WriteLine("Recieved invalid request type: {0}", request.Request.HttpMethod);
                    break;
            }

            request.Response.Close();
            Console.Title = "Kapture Server ::" +
                            " Gets: " + _numGets +
                            " - Posts: " + _numPosts +
                            " - Bandwidth: " + (_bandwidth/1024/1024) + "MB";
        }

        private static void HandlePost(HttpListenerContext c)
        {
            try
            {
                _numPosts++;
                var r = new BinaryReader(c.Request.InputStream);
                var fileName = RandomHash(HashSize) + "." + r.ReadString().ToLower();
                var file = r.ReadBytes(r.ReadInt32());
                r.Close();

                File.WriteAllBytes(DataDir + "/" + fileName, file);
                Console.WriteLine("Recieved POST for {0}byte file, storing at: {1}", file.Length, fileName);

                var response = Encoding.ASCII.GetBytes(Settings.GetValue<string>("url") + fileName);
                c.Response.OutputStream.Write(response, 0, response.Length);

                _bandwidth += response.Length + file.Length + fileName.Length;
            }
            catch (Exception e)
            {
                Console.WriteLine("POST handle failed: \n{0}", e);
            }
        }

        private static void HandleGet(HttpListenerContext c)
        {
            try
            {
                _numGets++;
                var requestedfile = DataDir + c.Request.Url.AbsolutePath;

                if (c.Request.Url.AbsolutePath == "" || //Home Page
                    c.Request.Url.AbsolutePath == "/")
                {
                    Console.WriteLine("Revieved GET for the home page.");
                    WriteBytes(c, GetHomeBitmap());   
                }
                else if (c.Request.Url.AbsolutePath.ToLower().StartsWith("/tunnel/")) //Tunnel
                {
                    string urlToLoad = 
                        c.Request.Url.AbsolutePath.ToLower()
                        .Replace("/tunnel/", "");

                    //Provisioning
                    if (!urlToLoad.StartsWith("http://") ||
                        !urlToLoad.StartsWith("https://"))
                        urlToLoad = "https://" + urlToLoad;
                    if (!urlToLoad.EndsWith("/"))
                        urlToLoad += "/";

                    Console.WriteLine("Recieved PROXY REQUEST for page: {0}", urlToLoad);

                    byte[] pageData;
                    try
                    {
                        pageData = new WebClient().DownloadData(urlToLoad);
                    }
                    catch
                    {
                        Console.WriteLine("Failed to access requested page!");
                        pageData = GetErrorBitmap("404", "Unable to access requested site");
                    }
                    WriteBytes(c, pageData);
                    //WebResponse response = WebRequest.Create(urlToLoad).GetResponse();
                    //WriteBytes(c, ReadFully(response.GetResponseStream()));
                    Console.WriteLine("\tReturned {0} bytes", pageData.Length);
                }
                else if (c.Request.Url.AbsolutePath.ToLower().StartsWith("/symlink/")) //Symbolic Link
                {
                    string symbolicName =
                        c.Request.Url.AbsolutePath.ToLower();
                        //.Replace("/symlink/", "");
                    string fileToLoad = Settings.GetValueUnsafe(symbolicName);

                    if (fileToLoad != null)
                    {
                        byte[] file = File.ReadAllBytes(fileToLoad);
                        WriteBytes(c, file);
                        Console.WriteLine("Received request for symbolic file {0}, returned {1} bytes", symbolicName, file.Length);
                    }
                    else
                    {
                        byte[] error = GetErrorBitmap("303", "Access denied for requested file.");
                        WriteBytes(c, error);
                        Console.WriteLine("Receive request for invalid symbolic link: {0}", symbolicName);
                    }
                }
                else if (c.Request.Url.AbsolutePath.ToLower().Contains("random")) //Random
                {
                    var possibleFiles = GetFiles(c.Request.Url.AbsolutePath.Split('.'));
                    var randomFile = possibleFiles[Random.Next(0, possibleFiles.Length - 1)];
                    Console.WriteLine("Recieved GET for random file, returned: {0}", randomFile);
                    WriteFile(c, randomFile);
                }
                else if (File.Exists(requestedfile)) //File
                {
                    Console.WriteLine("Recieved GET for {0}", requestedfile);
                    WriteFile(c, requestedfile);
                }
                else //Unknown
                {
                    Console.WriteLine("Recieved GET for non-existing file {0}", requestedfile);
                    WriteBytes(c, GetErrorBitmap("404", requestedfile.Replace("/uploads/", "") + " not found"));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("GET handle failed: \n{0}", e);
            }
        }

        public static byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static string[] GetFiles(string[] extensionData)
        {
            if (extensionData.Length < 2)
                return Directory.GetFiles(DataDir);
            var files = Directory.GetFiles(DataDir).Where(f => f.EndsWith(extensionData[1])).ToList();

            if (files.Count < 1)
                files.Add(DataDir + "favicon.ico");

            return files.ToArray();
        }

        private static void WriteFile(HttpListenerContext c, string requestedFile)
        {
            WriteBytes(c, File.ReadAllBytes(requestedFile));
        }

        private static void WriteBytes(HttpListenerContext c, byte[] bytes)
        {
            c.Response.OutputStream.Write(bytes, 0, bytes.Length);
            _bandwidth += bytes.Length;
        }

        //In the future, the HomePage will be an HTML file, not a bitmap
        private static byte[] GetHomeBitmap()
        {
            string about = Settings.GetValue<string>("about");
            var errorPage = new Bitmap(
                about.Length * 20 + 50, 200);
            var g = Graphics.FromImage(errorPage);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            g.DrawString(
                Settings.GetValue<string>("webPageName"),
                MainFont, Brushes.Gray, 10, 5);
            g.DrawString(about, SecondaryFont, Brushes.Gray, 25, 130);
            g.Save();

            byte[] imageBytes;
            using (var stream = new MemoryStream())
            {
                errorPage.Save(stream, ImageFormat.Png);
                stream.Close();

                imageBytes = stream.ToArray();
            }
            return imageBytes;
        }

        private static byte[] GetErrorBitmap(string errorCode, string error)
        {
            var errorPage = new Bitmap(500, 400);
            var g = Graphics.FromImage(errorPage);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            g.DrawString(errorCode, LargeFont, Brushes.LightGray, -20, 10);
            g.DrawString(Settings.GetValue<string>("webPageName"), MainFont, Brushes.Gray, 10, 5);
            g.DrawString(error, SecondaryFont, Brushes.Gray, 10, 300);
            g.Save();

            byte[] imageBytes;
            using (var stream = new MemoryStream())
            {
                errorPage.Save(stream, ImageFormat.Png);
                stream.Close();

                imageBytes = stream.ToArray();
            }
            return imageBytes;
        }

        private static readonly Settings Settings = new Settings();
        private static readonly string _font = Settings.GetValue<string>("font");
        private static readonly Font LargeFont = new Font(_font, 200.0f);
        private static readonly Font MainFont = new Font(_font, 60.0f);
        private static readonly Font SecondaryFont = new Font(_font, 20.0f);
    }
}