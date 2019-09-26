using NESSharp.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    public class PPU2C02
    {
        #region Fields
        private Cartridge _cartridge;

        /// <summary>
        /// V-Ram used to hold the name table information (2KB).
        /// Split into 2 - 1 KB chunks.
        /// </summary>
        private byte[,] _tblName;
        private byte[] _palette;
        public bool frameComplete;

        private Random _random;

        // represents the screen row
        private ushort _scanline;
        // represents the screen col
        private ushort _cycle;

        // Fields for debuggin information (e.g. visualization of memory etc.)
        /// <summary>
        /// This array stores all of the colors that the NES was capable of displaying
        /// </summary>
        public int[] palScreen { get; private set; }
        // Represents the NES fullscreen output (at NES resolution of 256, 240).
        public Sprite sprScreen { get; private set; }
        /// <summary>
        /// Represents a graphical depictation of the name tables memory.
        /// </summary>
        public Sprite[] sprNameTable { get; private set; }
        /// <summary>
        /// Represents a graphical depictation of the pattern tables memory.
        /// </summary>
        public Sprite[] sprPatternTable { get; private set; }
        #endregion

        #region Constructor
        public PPU2C02()
        {
            _tblName = new byte[2,1024];
            _palette = new byte[32];

            
            sprScreen = new Sprite(254, 240);
            sprNameTable = new Sprite[2] { new Sprite(256, 240), new Sprite(256, 240) };
            sprPatternTable = new Sprite[2] { new Sprite(128, 128), new Sprite(128, 128) };

            CreatePaletteColors();

            _random = new Random();

        }

        private void CreatePaletteColors()
        {
            // The following palette colours were taken from : 
            // http://wiki.nesdev.com/w/index.php/PPU_palettes
            // by blargg, NESTOPIA

            palScreen = new int[0x40];
            palScreen[0x00] = ColorHexFromRGB(84,  84,  84);
            palScreen[0x01] = ColorHexFromRGB(0,   30,  116);
            palScreen[0x02] = ColorHexFromRGB(8,   16,  144);
            palScreen[0x03] = ColorHexFromRGB(48,  0,   136);
            palScreen[0x04] = ColorHexFromRGB(68,  0,   100);
            palScreen[0x05] = ColorHexFromRGB(92,  0,   48);
            palScreen[0x06] = ColorHexFromRGB(84,  4,   0);
            palScreen[0x07] = ColorHexFromRGB(60,  24,  0);
            palScreen[0x08] = ColorHexFromRGB(32,  42,  0);
            palScreen[0x09] = ColorHexFromRGB(8,   58,  0);
            palScreen[0x0A] = ColorHexFromRGB(0,   64,  0);
            palScreen[0x0B] = ColorHexFromRGB(0,   60,  0);
            palScreen[0x0C] = ColorHexFromRGB(0,   50,  60);
            palScreen[0x0D] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x0E] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x0F] = ColorHexFromRGB(0,   0,   0);

            palScreen[0x10] = ColorHexFromRGB(152, 150, 152);
            palScreen[0x11] = ColorHexFromRGB(8,   76,  196);
            palScreen[0x12] = ColorHexFromRGB(48,  50,  236);
            palScreen[0x13] = ColorHexFromRGB(92,  30,  228);
            palScreen[0x14] = ColorHexFromRGB(136, 20,  176);
            palScreen[0x15] = ColorHexFromRGB(160, 20,  100);
            palScreen[0x16] = ColorHexFromRGB(152, 34,  32);
            palScreen[0x17] = ColorHexFromRGB(120, 60,  0);
            palScreen[0x18] = ColorHexFromRGB(84,  90,  0);
            palScreen[0x19] = ColorHexFromRGB(40,  114, 0);
            palScreen[0x1A] = ColorHexFromRGB(8,   124, 0);
            palScreen[0x1B] = ColorHexFromRGB(0,   118, 40);
            palScreen[0x1C] = ColorHexFromRGB(0,   102, 120);
            palScreen[0x1D] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x1E] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x1F] = ColorHexFromRGB(0,   0,   0);

            palScreen[0x20] = ColorHexFromRGB(236, 238, 236);
            palScreen[0x21] = ColorHexFromRGB(76,  154, 236);
            palScreen[0x22] = ColorHexFromRGB(120, 124, 236);
            palScreen[0x23] = ColorHexFromRGB(176, 98,  236);
            palScreen[0x24] = ColorHexFromRGB(228, 84,  236);
            palScreen[0x25] = ColorHexFromRGB(236, 88,  180);
            palScreen[0x26] = ColorHexFromRGB(236, 106, 100);
            palScreen[0x27] = ColorHexFromRGB(212, 136, 32);
            palScreen[0x28] = ColorHexFromRGB(160, 170, 0);
            palScreen[0x29] = ColorHexFromRGB(116, 196, 0);
            palScreen[0x2A] = ColorHexFromRGB(76,  208, 32);
            palScreen[0x2B] = ColorHexFromRGB(56,  204, 108);
            palScreen[0x2C] = ColorHexFromRGB(56,  180, 204);
            palScreen[0x2D] = ColorHexFromRGB(60,  60,  60);
            palScreen[0x2E] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x2F] = ColorHexFromRGB(0,   0,   0);

            palScreen[0x30] = ColorHexFromRGB(236, 238, 236);
            palScreen[0x31] = ColorHexFromRGB(168, 204, 236);
            palScreen[0x32] = ColorHexFromRGB(188, 188, 236);
            palScreen[0x33] = ColorHexFromRGB(212, 178, 236);
            palScreen[0x34] = ColorHexFromRGB(236, 174, 236);
            palScreen[0x35] = ColorHexFromRGB(236, 174, 212);
            palScreen[0x36] = ColorHexFromRGB(236, 180, 176);
            palScreen[0x37] = ColorHexFromRGB(228, 196, 144);
            palScreen[0x38] = ColorHexFromRGB(204, 210, 120);
            palScreen[0x39] = ColorHexFromRGB(180, 222, 120);
            palScreen[0x3A] = ColorHexFromRGB(168, 226, 144);
            palScreen[0x3B] = ColorHexFromRGB(152, 226, 180);
            palScreen[0x3C] = ColorHexFromRGB(160, 214, 228);
            palScreen[0x3D] = ColorHexFromRGB(160, 162, 160);
            palScreen[0x3E] = ColorHexFromRGB(0,   0,   0);
            palScreen[0x3F] = ColorHexFromRGB(0,   0,   0);

        }

        private int ColorHexFromARGB(byte a, byte r, byte g, byte b)
        {
            return ((a << 24) | (r << 16) | (g << 8) | b);
        }

        private int ColorHexFromRGB(byte r, byte g, byte b)
        {
            return ColorHexFromARGB(255, r, g, b);
        }

        #endregion

        #region Methods
        public void ConnectCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public void clock()
        {
            // Fake noise for now
            sprScreen.SetPixel(_cycle - 1, _scanline, palScreen[(_random.Next() % 2) == 0 ? 0x3F : 0x30]);

            // the ppu clock never stops
            _cycle++;

            // The following 'hard-coded' variables are defined by the actual NES hardware
            if(_cycle >= 341)
            {
                _cycle = 0;
                _scanline++;
                if(_scanline >= 261)
                {
                    _scanline = 0;
                    frameComplete = true;
                }
            }
        }

        /// <summary>
        /// Connects the PPU to the CPU's bus for reading RAM.
        /// </summary>
        public byte CPURead(ushort addr, bool isReadOnly)
        {
            byte data = 0x00;

            // The CPU can only address 8 different locations on the PPU
            switch (addr)
            {
                case 0x0000: // Control
                    break;
                case 0x0001: // Mask
                    break;
                case 0x0002: // Status
                    break;
                case 0x0003: // OAM Address
                    break;
                case 0x0004: // OAM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    break;
                case 0x0007: // PPU Data
                    break;

            }

            return data;
        }

        /// <summary>
        /// Connects the PPU to the CPU's bus for writing to RAM.
        /// </summary>
        public void CPUWrite(ushort addr, byte data)
        {
            // The CPU can only address 8 different locations on the PPU
            switch (addr)
            {
                case 0x0000: // Control
                    break;
                case 0x0001: // Mask
                    break;
                case 0x0002: // Status
                    break;
                case 0x0003: // OAM Address
                    break;
                case 0x0004: // OAM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    break;
                case 0x0007: // PPU Data
                    break;

            }
        }

        /// <summary>
        /// Connects the PPU to its own bus.
        /// </summary>
        public byte PPURead(ushort addr, bool isReadOnly)
        {
            byte data = 0x00;
            addr &= 0x3FFF;

            return data;
        }

        /// <summary>
        /// Connects the PPU to its own bus.
        /// </summary>
        public void PPUWrite(ushort addr, byte data)
        {
            addr &= 0x3FFF;

        }
        #endregion

    }
}
