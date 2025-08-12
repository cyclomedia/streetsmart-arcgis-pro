using System;
using Aspose.Drawing;
using Aspose.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AsposePixelformat = Aspose.Drawing.Imaging.PixelFormat;

namespace StreetSmartArcGISPro.Utilities
{
  public static class WpfInterop
  {
    /// <summary>
    /// Converts an Aspose.Drawing.Bitmap to a WPF BitmapSource without recompression.
    /// Works fastest with 32bpp (P)ARGB. Other formats are temporarily converted to 32bppPArgb.
    /// </summary>
    public static BitmapSource ToBitmapSourceFast(Bitmap src)
    {
      if (src == null)
      {
        throw new ArgumentNullException(nameof(src));
      }

      // Select target pixel formats (WPF and Aspose)
      var wpfPixelFormat = PixelFormats.Pbgra32; // fits 32bppPArgb (premultiplied)
      var desiredAsposeFormat = AsposePixelformat.Format32bppPArgb;

      Bitmap working = src;
      bool createdTemp = false;

      // Zorg dat we in een 32bpp(P)ARGB-formaat zitten, anders even clonen
      if (src.PixelFormat != AsposePixelformat.Format32bppPArgb &&
          src.PixelFormat != AsposePixelformat.Format32bppArgb)
      {
        working = src.Clone(new Rectangle(0, 0, src.Width, src.Height), desiredAsposeFormat);
        createdTemp = true;
      }

      try
      {
        // If it's 32bpp ARGB (non-premultiplied), then grab Bgra32
        if (working.PixelFormat == AsposePixelformat.Format32bppArgb)
          wpfPixelFormat = PixelFormats.Bgra32;
        else
          wpfPixelFormat = PixelFormats.Pbgra32; // for 32bppPArgb

        var rect = new Rectangle(0, 0, working.Width, working.Height);
        var data = working.LockBits(rect, ImageLockMode.ReadOnly, working.PixelFormat);

        try
        {
          int width = working.Width;
          int height = working.Height;
          int stride = Math.Abs(data.Stride);
          int bufferSize = stride * height;

          // WPF copies the data internally, so the pointer can be released afterwards.
          var bs = BitmapSource.Create(
              width,
              height,
              96.0,     // dpiX
              96.0,     // dpiY
              wpfPixelFormat,
              null,     // no palette needed at 32bpp
              data.Scan0,
              bufferSize,
              stride
          );

          bs.Freeze(); // thread-safe
          return bs;
        }
        finally
        {
          working.UnlockBits(data);
        }
      }
      finally
      {
        if (createdTemp)
        {
          working.Dispose();
        }
      }
    }
  }
}
