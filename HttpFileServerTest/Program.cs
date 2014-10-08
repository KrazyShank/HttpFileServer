using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpFileServerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            WebClient wc = new WebClient();

            //wc.UploadData("http://localhost:8080", "POST", File.ReadAllBytes("C:/Users/Alec/Desktop/sweq.jpg"));
            byte[] response = wc.DownloadData("http://localhost:8080/sweq.jpg");
           // byte[] responce = wc.UploadData("http://localhost:8080", "GET", Encoding.ASCII.GetBytes("sweq.png"));

            Image.FromStream(new MemoryStream(response)).Save("C:/Users/Alec/Desktop/result.jpg");
        }
    }
}
