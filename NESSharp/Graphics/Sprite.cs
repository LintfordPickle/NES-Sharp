using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Graphics
{
    /// <summary>
    /// A bitmap like structure which stores a 2d array of pixels
    /// </summary>
    public class Sprite
    {
        #region Variables
        public int width { get; protected set; }
        public int height { get; protected set; }

        /// <summary>
        /// Represents the internal texel data for this Sprite
        /// </summary>
        public readonly int[] bitmap;
        #endregion

        #region Constructor
        public Sprite(int width, int height)
        {
            this.width = width;
            this.height = height;

            bitmap = new int[width * height];
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns the texel col value at the given coord (in texture space).
        /// </summary>
        public int GetPixel(int x, int y)
        {
            if (x < 0 || x >= width) return 0x00000000;
            if (y < 0 || y >= height) return 0x00000000;

            return bitmap[y * width + x];

        }

        /// <summary>
        /// Sets the given texel to the col value.
        /// </summary>
        public void SetPixel(int x, int y, int col)
        {
            if (x < 0 || x >= width) return;
            if (y < 0 || y >= height) return;

            bitmap[y * width + x] = col;

        }
        #endregion
    }
}
