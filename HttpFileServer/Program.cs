using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using log4net.Config;

[assembly: XmlConfigurator(Watch = true)]

namespace HttpFileServer
{
    internal class Program
    {
        private const string DataDir = "/Uploads";
        private const int HashSize = 8;

        private static readonly ILog Log = LogManager.GetLogger
            (MethodBase.GetCurrentMethod().DeclaringType);

        //static string[] PREFIXES = { "http://localhost:8080/" };
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

            Log.InfoFormat("Mounted data directory at: " + DataDir);

            _listener = new HttpListener();
            foreach (var p in Prefixes)
                _listener.Prefixes.Add(p);

            _listener.Start();
            _listener.BeginGetContext((BeginGetCallback), null);

            Log.InfoFormat("HTTP Listener started listening for " + Prefixes.Count() + " prefixes.");
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
                    Log.WarnFormat("Recieved invalid request type: {0}", request.Request.HttpMethod);
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
                Log.InfoFormat("Recieved POST for {0}byte file, storing at: {1}", file.Length, fileName);

                var response = Encoding.ASCII.GetBytes(Settings.GetValue<string>("url") + fileName);
                c.Response.OutputStream.Write(response, 0, response.Length);

                _bandwidth += response.Length + file.Length + fileName.Length;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("POST handle failed: \n{0}", e);
            }
        }

        private static void HandleGet(HttpListenerContext c)
        {
            try
            {
                _numGets++;
                var requestedfile = DataDir + c.Request.Url.AbsolutePath;

                if (c.Request.Url.AbsolutePath.ToLower().Contains("random"))
                {
                    var possibleFiles = GetFiles(c.Request.Url.AbsolutePath.Split('.'));

                    var randomFile = possibleFiles[Random.Next(0, possibleFiles.Length - 1)];

                    Log.WarnFormat("Recieved GET for random file, returned: {0}", randomFile);

                    WriteFile(randomFile, c.Response.OutputStream);
                }
                else if (File.Exists(requestedfile))
                {
                    Log.InfoFormat("Recieved GET for {0}", requestedfile);
                    WriteFile(requestedfile, c.Response.OutputStream);
                }
                else
                {
                    Log.WarnFormat("Recieved GET for non-existing file {0}", requestedfile);
                    var file = GetErrorBitmap(requestedfile);
                    c.Response.OutputStream.Write(file, 0, file.Length);
                    _bandwidth += file.Length;
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat("GET handle failed: \n{0}", e);
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

        private static void WriteFile(string requestedFile, Stream output)
        {
            var file = File.ReadAllBytes(requestedFile);
            output.Write(file, 0, file.Length);
            _bandwidth += file.Length;
        }

        private static byte[] GetErrorBitmap(string requestedFile)
        {
            var errorPage = new Bitmap(500, 400);
            var g = Graphics.FromImage(errorPage);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            g.DrawString("404", LargeFont, Brushes.LightGray, -20, 10);
            g.DrawString(Settings.GetValue<string>("webPageName"), MainFont, Brushes.Gray, 10, 5);
            g.DrawString(requestedFile + " not found", SecondaryFont, Brushes.Gray, 10, 300);
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