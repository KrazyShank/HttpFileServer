using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace HttpFileServer
{
    class Program
    {
        const string DataDirectory = "/Uploads";
        const string Prefix = "http://localhost:8080/";
        static HttpListener Listener;

        static void Main(string[] args)
        {
            if (Directory.Exists(DataDirectory))
                Directory.CreateDirectory(DataDirectory);

            Listener = new HttpListener();
            Listener.Prefixes.Add(Prefix);

            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(BeginGetCallback), null);
            Console.WriteLine("HTTP Listener started listening for: " + Prefix);
            Console.ReadLine();
        }

        private static void BeginGetCallback(IAsyncResult result)
        {
            HttpListenerContext request = (Listener.EndGetContext(result) as HttpListenerContext);

            if (request.Request.HttpMethod.ToUpper() == "POST")
                HandlePost(request);
            else if (request.Request.HttpMethod.ToUpper() == "GET")
                HandleGet(request);
            else
                Console.WriteLine("Recieved invalid request type: " + request.Request.HttpMethod);

            request.Response.Close();
        }

        private static void HandlePost(HttpListenerContext c)
        {

        }

        private static void HandleGet(HttpListenerContext c)
        {
            string requestedfile = DataDirectory + c.Request.Url.AbsolutePath;

            if (File.Exists(requestedfile))
            {
                Console.WriteLine("Recieved GET for " + requestedfile);
                byte[] file = File.ReadAllBytes(requestedfile);
                c.Response.OutputStream.Write(file, 0, file.Length);
            }
            else
            {
                Console.WriteLine("Recieved GET for non-existing file " + requestedfile);
                byte[] file = GetErrorBitmap(requestedfile);
                c.Response.OutputStream.Write(file, 0, file.Length);
            }
        }

        static Font MainFont = new Font("Segoe UI", 30.0f);
        static Font SecondaryFont = new Font("Segoe UI", 15.0f);
        private static byte[] GetErrorBitmap(string requestedFile)
        {
            Bitmap errorPage = new Bitmap(800, 400);
            Graphics g = Graphics.FromImage(errorPage);

            g.DrawString("Kronks.me 404", MainFont, Brushes.Gray, 5, 5);
            g.DrawString(requestedFile, MainFont, Brushes.Gray, 5, 200);
            g.Save();

            byte[] imageBytes = null;
            using (MemoryStream stream = new MemoryStream())
            {
                errorPage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Close();

                imageBytes = stream.ToArray();
            }
            return imageBytes;
        }
    }
}
