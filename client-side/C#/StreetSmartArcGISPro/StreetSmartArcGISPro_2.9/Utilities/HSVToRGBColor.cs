using System;

namespace StreetSmartArcGISPro.Utilities
{
  public struct Rgb
  {
    public Rgb(byte r, byte g, byte b)
    {
      R = r;
      G = g;
      B = b;
    }

    public byte R { get; set; }

    public byte G { get; set; }

    public byte B { get; set; }
  }

  public struct Hsv
  {
    public Hsv(double h, double s, double v)
    {
      H = h;
      S = s;
      V = v;
    }

    public double H { get; set; }

    public double S { get; set; }

    public double V { get; set; }
  }

  public static class ColorConverter
  {
    public static Rgb HsvToRgb(Hsv hsv)
    {
      double r = 0, g = 0, b = 0;

      if (hsv.S == 0)
      {
        r = hsv.V;
        g = hsv.V;
        b = hsv.V;
      }
      else
      {
        int i;
        double f, p, q, t;

        if (Math.Abs(hsv.H - 360) < 0.0001)
        {
          hsv.H = 0;
        }
        else
        {
          hsv.H = hsv.H / 60;
        }

        i = (int) Math.Truncate(hsv.H);
        f = hsv.H - i;

        p = hsv.V * (1.0 - hsv.S);
        q = hsv.V * (1.0 - hsv.S * f);
        t = hsv.V * (1.0 - hsv.S * (1.0 - f));

        switch (i)
        {
          case 0:
            r = hsv.V;
            g = t;
            b = p;
            break;

          case 1:
            r = q;
            g = hsv.V;
            b = p;
            break;

          case 2:
            r = p;
            g = hsv.V;
            b = t;
            break;

          case 3:
            r = p;
            g = q;
            b = hsv.V;
            break;

          case 4:
            r = t;
            g = p;
            b = hsv.V;
            break;

          default:
            r = hsv.V;
            g = p;
            b = q;
            break;
        }
      }

      return new Rgb((byte) (r * 255), (byte) (g * 255), (byte) (b * 255));
    }
  }
}
