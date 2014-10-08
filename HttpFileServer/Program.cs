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
        static HttpListener Listener;
        static void Main(string[] args)
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://localhost:8080/");
            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(BeginGetCallback), null);
            Console.ReadLine();
        }

        private static void BeginGetCallback(IAsyncResult result)
        {
            HttpListenerContext request = (Listener.EndGetContext(result) as HttpListenerContext);

            if (request.Request.HttpMethod.ToUpper() == "POST")
            {
                Console.WriteLine("POST");

                Image.FromStream(request.Request.InputStream).Save("C:/Users/Alec/Desktop/result.png");
            }
            else if (request.Request.HttpMethod.ToUpper() == "GET")
            {
                Console.WriteLine("GET");
                string requestedfile = request.Request.Url.AbsolutePath;

                if (requestedfile == "/sweq.jpg")
                {
                    byte[] file = File.ReadAllBytes("C:/Users/Alec/Desktop/sweq.jpg");
                    request.Response.OutputStream.Write(file, 0, file.Length);
                }
            }
            else
                Console.WriteLine(request.Request.HttpMethod);

            request.Response.Close();
        }
    }
}
