using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NESSharpWinForm
{   
    /// <summary>
    /// A wrapper for the System.Drawing.Bitmap to allow us to directly access the underlying bitmap array.
    /// </summary>
    public class DirectBitmap
    {
        #region Variables
        public Bitmap bitmap { get; private set; }
        public int[] pixels { get; private set; }
        public bool isDisposed { get; private set; }
        public int height { get; private set; }
        public int width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }
        #endregion

        #region Constructor
        public DirectBitmap(int width, int height, PixelFormat pixelFormat)
        {
            this.width = width;
            this.height = height;
            pixels = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            bitmap = new Bitmap(width, height, width * 4, pixelFormat, BitsHandle.AddrOfPinnedObject());

            // Clear the buffer to black (0xff000000)
            FillColor(Color.Black.ToArgb());
        }
        #endregion

        #region Methods
        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= width) return;
            if (y < 0 || y >= height) return;

            int index = x + (y * width);
            int col = color.ToArgb();

            pixels[index] = col;

        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || x >= width) return Color.Magenta;
            if (y < 0 || y >= height) return Color.Magenta;

            int index = x + (y * width);
            int col = pixels[index];

            return Color.FromArgb(col);
        }

        public void FillColor(int col)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = col;
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            bitmap.Dispose();
            BitsHandle.Free();
        }
        #endregion

    }
}
