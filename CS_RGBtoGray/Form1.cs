using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS_RGBtoGray
{
    //https://blog.holey.cc/2018/04/18/csharp-three-way-covert-bmp-rgb-to-gray/
    //http://hk.uwenku.com/question/p-qnyytjbn-bco.html
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public Bitmap RGBtoGray01(Bitmap bmpSrc)
        {
            Bitmap bitmap = new Bitmap(bmpSrc);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int gray = (
                        bitmap.GetPixel(x, y).R +
                        bitmap.GetPixel(x, y).G +
                        bitmap.GetPixel(x, y).B) / 3;
                    Color color = Color.FromArgb(gray, gray, gray);
                    bitmap.SetPixel(x, y, color);
                }
            }
            return bitmap;
        }

        public Bitmap RGBtoGray02(Bitmap bmpSrc)
        {
            Bitmap bitmap = new Bitmap(bmpSrc);
            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            unsafe
            {
                int stride = bmpData.Stride;
                int offset = stride - bitmap.Width * 3;

                IntPtr intPtr = bmpData.Scan0;
                byte* p = (byte*)(void*)intPtr;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        byte gray = (byte)((
                            p[y * (bitmap.Width * 3 + offset) + (x * 3) + 0] +
                            p[y * (bitmap.Width * 3 + offset) + (x * 3) + 1] +
                            p[y * (bitmap.Width * 3 + offset) + (x * 3) + 2]) / 3);
                        for (int i = 0; i < 3; i++)
                        {
                            p[y * (bitmap.Width * 3 + offset) + (x * 3) + i] = gray;
                        }
                    }
                }
            }

            bitmap.UnlockBits(bmpData);
            return bitmap;
        }

        public Bitmap RGBtoGray03(Bitmap bmpSrc)
        {
            ColorMatrix cm = new ColorMatrix(new float[][]
            {
                new float[] { 0.30f, 0.30f, 0.30f, 0.00f, 0.00f } ,
                new float[] { 0.59f, 0.59f, 0.59f, 0.00f, 0.00f } ,
                new float[] { 0.11f, 0.11f, 0.11f, 0.00f, 0.00f } ,
                new float[] { 0.00f, 0.00f, 0.00f, 1.00f, 0.00f } ,
                new float[] { 0.00f, 0.00f, 0.00f, 0.00f, 1.00f }
            });
            ImageAttributes ia = new ImageAttributes();
            ia.SetColorMatrix(cm);

            Bitmap bitmap = new Bitmap(bmpSrc.Width, bmpSrc.Height, bmpSrc.PixelFormat);
            Graphics g = Graphics.FromImage(bitmap);
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            g.DrawImage(bmpSrc, rect, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, ia);
            return bitmap;
        }

        public Bitmap ConvertTo1Bit(Bitmap input)
        {
            var masks = new byte[] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
            var output = new Bitmap(input.Width, input.Height, PixelFormat.Format1bppIndexed);
            var data = new sbyte[input.Width, input.Height];
            var inputData = input.LockBits(new Rectangle(0, 0, input.Width, input.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var scanLine = inputData.Scan0;
                var line = new byte[inputData.Stride];
                for (var y = 0; y < inputData.Height; y++, scanLine += inputData.Stride)
                {
                    Marshal.Copy(scanLine, line, 0, line.Length);
                    for (var x = 0; x < input.Width; x++)
                    {
                        data[x, y] = (sbyte)(64 * (GetGreyLevel(line[x * 3 + 2], line[x * 3 + 1], line[x * 3 + 0]) - 0.5));
                    }
                }
            }
            finally
            {
                input.UnlockBits(inputData);
            }

            var outputData = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
            try
            {
                var scanLine = outputData.Scan0;
                for (var y = 0; y < outputData.Height; y++, scanLine += outputData.Stride)
                {
                    var line = new byte[outputData.Stride];
                    for (var x = 0; x < input.Width; x++)
                    {
                        var j = data[x, y] > 0;
                        if (j) line[x / 8] |= masks[x % 8];
                        var error = (sbyte)(data[x, y] - (j ? 32 : -32));
                        if (x < input.Width - 1) data[x + 1, y] += (sbyte)(7 * error / 16);
                        if (y < input.Height - 1)
                        {
                            if (x > 0) data[x - 1, y + 1] += (sbyte)(3 * error / 16);
                            data[x, y + 1] += (sbyte)(5 * error / 16);
                            if (x < input.Width - 1) data[x + 1, y + 1] += (sbyte)(1 * error / 16);
                        }
                    }
                    Marshal.Copy(line, 0, scanLine, outputData.Stride);
                }
            }
            finally
            {
                output.UnlockBits(outputData);
            }
            return output;
        }

        public double GetGreyLevel(byte r, byte g, byte b)
        {
            return (r * 0.299 + g * 0.587 + b * 0.114) / 255;
        }

        public Bitmap BitmapTo1Bpp(Bitmap img)
        {
            int w = img.Width;
            int h = img.Height;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format1bppIndexed);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format1bppIndexed);
            byte[] scan = new byte[(w + 7) / 8];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x % 8 == 0) scan[x / 8] = 0;
                    Color c = img.GetPixel(x, y);
                    if (c.GetBrightness() >= 0.7) scan[x / 8] |= (byte)(0x80 >> (x % 8));
                }
                Marshal.Copy(scan, 0, (IntPtr)((long)data.Scan0 + data.Stride * y), scan.Length);
            }
            bmp.UnlockBits(data);
            return bmp;
        }
        private void Form1_Load(object sender, EventArgs e)
        {

            Bitmap RGBtoGray = RGBtoGray03(new Bitmap("20210610094646.jpg"));
            pictureBox1.Image = RGBtoGray;

            pictureBox2.Image = BitmapTo1Bpp(new Bitmap("20210610094646.jpg"));

        }

    }
}
