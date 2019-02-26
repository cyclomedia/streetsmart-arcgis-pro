/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018 - 2019, CycloMedia, All rights reserved.
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3.0 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library.
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using DrawingImage = System.Drawing.Image;
using DrawingBitmap = System.Drawing.Bitmap;

namespace StreetSmartArcGISPro.Utilities
{
  static class ImageExtensions
  {
    public static BitmapSource ToBitmapSource(this DrawingImage source)
    {
      DrawingBitmap bitmap = new DrawingBitmap(source);
      var bitSrc = bitmap.ToBitmapSource();
      bitmap.Dispose();
      return bitSrc;
    }

    public static BitmapSource ToBitmapSource(this DrawingBitmap source)
    {
      BitmapSource bitSrc;
      var hBitmap = source.GetHbitmap();

      try
      {
        bitSrc = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
          BitmapSizeOptions.FromEmptyOptions());
      }
      catch (Win32Exception)
      {
        bitSrc = null;
      }
      finally
      {
        DeleteObject(hBitmap);
      }

      return bitSrc;
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);
  }
}
