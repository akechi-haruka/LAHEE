using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace LAHEE.Util;

class Utils {
    private static readonly Random RANDOM = new Random();

    public static long CurrentUnixSeconds {
        get { return (long)(DateTime.Now.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds; }
    }

    public static string RandomString(int length) {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[RANDOM.Next(s.Length)]).ToArray());
    }

    [SupportedOSPlatform("windows")]
    public static Bitmap ResizeImage(Image image, int width, int height) {
        Rectangle destRect = new Rectangle(0, 0, width, height);
        Bitmap destImage = new Bitmap(width, height);

        destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        using (Graphics graphics = Graphics.FromImage(destImage)) {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var wrapMode = new ImageAttributes()) {
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }
        }

        return destImage;
    }

    [SupportedOSPlatform("windows")]
    public static Bitmap MakeGrayscale3(Bitmap original) {
        //create a blank bitmap the same size as original
        Bitmap newBitmap = new Bitmap(original.Width, original.Height);

        //get a graphics object from the new image
        using (Graphics g = Graphics.FromImage(newBitmap)) {
            //create the grayscale ColorMatrix
            ColorMatrix colorMatrix = new ColorMatrix(
                new float[][] {
                    new float[] { .3f, .3f, .3f, 0, 0 },
                    new float[] { .59f, .59f, .59f, 0, 0 },
                    new float[] { .11f, .11f, .11f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });

            //create some image attributes
            using (ImageAttributes attributes = new ImageAttributes()) {
                //set the color matrix attribute
                attributes.SetColorMatrix(colorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        return newBitmap;
    }

    public static void OpenBrowser(string url) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            Process.Start("xdg-open", url);
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            Process.Start("open", url);
        } else {
            throw new NotSupportedException("Not supported for this OS");
        }
    }

    public static double ConvertToUnixTimestamp(DateTime date) {
        DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan diff = date.ToUniversalTime() - origin;
        return Math.Floor(diff.TotalSeconds);
    }

    public static string MD5(string data) {
        using (MD5 md5 = System.Security.Cryptography.MD5.Create()) {
            byte[] inputBytes = Encoding.ASCII.GetBytes(data);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}