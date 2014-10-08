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

            string extension = "jpg";
            byte[] data = File.ReadAllBytes("C:/Users/Alec/Desktop/sweq.jpg");

            MemoryStream s = new MemoryStream();
            byte[] b;

            using (BinaryWriter sr = new BinaryWriter(s))
            {
                sr.Write(extension);
                sr.Write(data.Length);
                sr.Write(data);
                b = s.ToArray();
            }
            Process.Start(Encoding.ASCII.GetString(wc.UploadData("http://localhost:8080", "POST", b)));
        }
    }
}
