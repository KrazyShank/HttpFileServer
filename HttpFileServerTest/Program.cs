using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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

            string extension = "png";
            byte[] data = File.ReadAllBytes("C:/Users/Alec/Desktop/sweq.png");

            MemoryStream s = new MemoryStream();
            byte[] b;

            using (BinaryWriter sr = new BinaryWriter(s))
            {
                sr.Write(extension);
                sr.Write(data.Length);
                sr.Write(data);
                b = s.ToArray();
            }

            byte[] response = wc.UploadData("http://71.231.167.96/", "POST", b);
            string url = Encoding.ASCII.GetString(response);
            Console.WriteLine(url);
            if (url.EndsWith(extension))
                Process.Start(url);

            Console.ReadLine();
        }
    }
}
