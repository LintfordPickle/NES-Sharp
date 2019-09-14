using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NESSharp.Graphics
{   
    /// <summary>
    /// A wrapper for the System.Drawing.Bitmap to allow us to directly access the underlying bitmap array.
    /// </summary>
    public class DirectBitmap
    {
        #region Variables
        public Bitmap Bitmap { get; private set; }
        public Int32[] Bits { get; private set; }
        public bool Disposed { get; private set; }
        public int Height { get; private set; }
        public int Width { get; private set; }

        protected GCHandle BitsHandle { get; private set; }
        #endregion

        #region Constructor
        public DirectBitmap(int width, int height, PixelFormat pixelFormat)
        {
            Width = width;
            Height = height;
            Bits = new Int32[width * height];
            BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
            Bitmap = new Bitmap(width, height, width * 4, pixelFormat, BitsHandle.AddrOfPinnedObject());

            // Clear the buffer to black (0xff000000)
            FillColor(Color.Black.ToArgb());
        }
        #endregion

        #region Methods
        public void SetPixel(int x, int y, Color color)
        {
            // TODO: protect bounds

            int index = x + (y * Width);
            int col = color.ToArgb();

            Bits[index] = col;
        }

        public Color GetPixel(int x, int y)
        {
            // TODO: protect bounds

            int index = x + (y * Width);
            int col = Bits[index];

            return Color.FromArgb(col);
        }

        public void FillColor(int col)
        {
            for (int i = 0; i < Bits.Length; i++)
            {
                Bits[i] = col;
            }
        }

        public void Dispose()
        {
            if (Disposed) return;

            Disposed = true;
            Bitmap.Dispose();
            BitsHandle.Free();
        }
        #endregion

    }
}
