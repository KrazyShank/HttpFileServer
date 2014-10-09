using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Text;
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
        const string DATA_DIR = "/Uploads";
        //static string[] PREFIXES = { "http://localhost:8080/" };
        static string[] PREFIXES = { "http://+:80/" };
        static HttpListener Listener;

        const int HASH_SIZE = 8;

        static int NumGets = 0;
        static int NumPosts = 0;
        static long Bandwidth = 0; //bytes

        static void Main(string[] args)
        {
            Console.Title = "Kapture Server :: Gets: 0 - Posts: 0 - Bandwidth: 0MB";
            if (!Directory.Exists(DATA_DIR))
                Directory.CreateDirectory(DATA_DIR);

            Console.WriteLine("Mounted data directory at: " + DATA_DIR);

            Listener = new HttpListener();
            foreach (string p in PREFIXES)
                Listener.Prefixes.Add(p);

            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(BeginGetCallback), null);
            Console.WriteLine("HTTP Listener started listening for " + PREFIXES.Count() + " prefixes.");
            Console.ReadLine();
        }

        private static Random Random = new Random();
        public static string RandomHash(int length)
        {
            string characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            StringBuilder result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(characters[Random.Next(characters.Length)]);
            }
            return result.ToString();
        }

        private static void BeginGetCallback(IAsyncResult result)
        {
            Listener.BeginGetContext(new AsyncCallback(BeginGetCallback), null);
            HttpListenerContext request = (Listener.EndGetContext(result) as HttpListenerContext);

            if (request.Request.HttpMethod.ToUpper() == "POST")
                HandlePost(request);
            else if (request.Request.HttpMethod.ToUpper() == "GET")
                HandleGet(request);
            else
                Console.WriteLine("Recieved invalid request type: " + request.Request.HttpMethod);

            request.Response.Close();
            Console.Title = "Kapture Server ::" + 
                " Gets: " + NumGets + 
                " - Posts: " + NumPosts + 
                " - Bandwidth: " + (Bandwidth / 1024 / 1024) + "MB";
        }

        private static void HandlePost(HttpListenerContext c)
        {
            try
            {
                NumPosts++;
                BinaryReader r = new BinaryReader(c.Request.InputStream);
                string fileName = RandomHash(HASH_SIZE) + "." + r.ReadString();
                byte[] file = r.ReadBytes(r.ReadInt32());
                r.Close();

                File.WriteAllBytes(DATA_DIR + "/" + fileName, file);
                Console.WriteLine("Recieved POST for " + file.Length + "byte file, storing at: " + fileName);

                byte[] response = Encoding.ASCII.GetBytes("http://kronks.me/" + fileName);
                c.Response.OutputStream.Write(response, 0, response.Length);

                Bandwidth += response.Length + file.Length + fileName.Length;
            }
            catch (Exception e)
            {
                Console.WriteLine("Post handle failed: \n" + e.ToString());
            }
        }

        private static void HandleGet(HttpListenerContext c)
        {
            try
            {
                NumGets++;
                string requestedfile = DATA_DIR + c.Request.Url.AbsolutePath;

                if (c.Request.Url.AbsolutePath.ToLower().Contains("random"))
                {
                    string[] possibleFiles = GetFiles(c.Request.Url.AbsolutePath.Split('.'));

                    string randomFile = possibleFiles[Random.Next(0, possibleFiles.Length - 1)];

                    Console.WriteLine("Recieved GET for random file, returned: " + randomFile);

                    WriteFile(randomFile, c.Response.OutputStream);
                }
                else if (File.Exists(requestedfile))
                {
                    Console.WriteLine("Recieved GET for " + requestedfile);
                    WriteFile(requestedfile, c.Response.OutputStream);
                }
                else
                {
                    Console.WriteLine("Recieved GET for non-existing file " + requestedfile);
                    byte[] file = GetErrorBitmap(requestedfile);
                    c.Response.OutputStream.Write(file, 0, file.Length);
                    Bandwidth += file.Length;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Get handle failed: \n" + e.ToString());
            }
        }

        public static string[] GetFiles(string[] extensionData)
        {
            if (extensionData.Length < 2)
                return Directory.GetFiles(DATA_DIR);
            else
            {
                List<string> files = new List<string>();
                foreach (string f in Directory.GetFiles(DATA_DIR))
                    if (f.EndsWith(extensionData[1]))
                        files.Add(f);

                if (files.Count < 1)
                    files.Add(DATA_DIR + "favicon.ico");

                return files.ToArray();
            }
        }

        private static void WriteFile(string requestedFile, Stream output)
        {
            byte[] file = File.ReadAllBytes(requestedFile);
            output.Write(file, 0, file.Length);
            Bandwidth += file.Length;
        }

        static Font LargeFont = new Font("Segoe UI", 200.0f);
        static Font MainFont = new Font("Segoe UI", 60.0f);
        static Font SecondaryFont = new Font("Segoe UI", 20.0f);
        private static byte[] GetErrorBitmap(string requestedFile)
        {
            Bitmap errorPage = new Bitmap(500, 400);
            Graphics g = Graphics.FromImage(errorPage);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            g.DrawString("404", LargeFont, Brushes.LightGray, -20, 10);
            g.DrawString("Kronks.me", MainFont, Brushes.Gray, 10, 5);
            g.DrawString(requestedFile + " not found", SecondaryFont, Brushes.Gray, 10, 300);
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
