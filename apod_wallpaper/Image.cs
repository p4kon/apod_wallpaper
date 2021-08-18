using System;
using System.Net;
//using System.Net.Http;
using System.Web;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

namespace apod_wallpaper
{
    class Image
    {
        private static Bitmap bitmap;
        private string image_url;
        public string image_path = @"images\";
        public readonly static string png_format = ".png";
        public readonly static string jpg_format = ".jpg";
        public ImageFormat format;
        public string name;

        public Image(string image_url, string name)
        {
            this.image_url = image_url;
            this.format = System.Drawing.Imaging.ImageFormat.Jpeg;
            this.name = name;
        }

        public Bitmap GetImage()
        {
            return bitmap;
        }

        public void SaveImage(string filename, ImageFormat format)
        {
            if (bitmap != null)
            {   
                bitmap.Save(image_path + filename, format);
            }
        }

        public void DownloadImage()
        {
            WebRequest request = WebRequest.Create(image_url);
            WebResponse response = request.GetResponse();
            using (WebClient client = new WebClient())
            {
                Network.SetCredentails(client);
                using (Stream dataStream = response.GetResponseStream())
                {
                    bitmap = new Bitmap(dataStream);
                    dataStream.Flush();
                    dataStream.Close();
                }
                response.Close();
            }
        }
    }
}
