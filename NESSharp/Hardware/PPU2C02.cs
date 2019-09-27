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
        private byte _ppuMaskReg;
        private byte _ppuStatusReg;
        private byte _ppuControlReg;

        // reading/writting to the loow or high byte
        private byte _addressLatch = 0x00;
        private byte _ppuDataBuffer = 0x00;
        private ushort _ppuAddress = 0x0000;

        public enum FLAGS2C02_Status
        {
            VerticalBlank = (1 << 7),  // Is the PPU in 'Screen-Space' or the period of V-Blank?
            SpriteZeroHit = (1 << 6), 
            SpriteOverflow = (1 << 5), 
            Unused4 = (1 << 4),        // Unused
            Unused3 = (1 << 3),        // Unused
            Unused2 = (1 << 2),        // Unused
            Unused1 = (1 << 1),        // Unused
            Unused0 = (1 << 0),        // Unused
        }

        /// <summary>
        /// These flags determine which parts of the PPU are switched on or off.
        /// </summary>
        public enum FLAGS2C02_Mask
        {
            GrayScale = (1 << 7), 
            RenderBackgroundLeft = (1 << 6), 
            RenderSpriteLeft = (1 << 5), 
            RenderBackground = (1 << 4), 
            RenderSprites = (1 << 3), 
            EnhanceRed = (1 << 2),   
            EnhanceGreen = (1 << 1), 
            EnhanceBlue = (1 << 0),  
        }

        public enum FLAGS2C02_Control
        {
            NametableX = (1 << 7),
            NametableY = (1 << 6),
            IncrementMode = (1 << 5),
            PatternSprite = (1 << 4),
            PatternBackground = (1 << 3),
            SpriteSize = (1 << 2),
            SlaveMode = (1 << 1),
            EnableNMI = (1 << 0),        // Emit NMI?
        }

        /// <summary>
        /// Helper function for setting the bits of the ppu status register.
        /// </summary>
        /// <param name="flag">The flag to set</param>
        /// <param name="v">The value of the bit [0,1]</param>
        private void SetStatusFlag(FLAGS2C02_Status flag, bool v)
        {
            if (v)
                _ppuStatusReg |= (byte)flag;
            else
                _ppuStatusReg &= (byte)~flag;
        }

        /// <summary>
        /// Helper function to get the state of a specific bit of the status register.
        /// </summary>
        /// <param name="flag">The bit flag to return</param>
        /// <returns>1 or 0</returns>
        public bool GetStatusFlag(FLAGS2C02_Status flag)
        {
            return (byte)(_ppuStatusReg & (byte)flag) > 0 ? true : false;

        }

        /// <summary>
        /// Helper function for setting the bits of the ppu mask register.
        /// </summary>
        /// <param name="flag">The flag to set</param>
        /// <param name="v">The value of the bit [0,1]</param>
        private void SetMaskRegFlag(FLAGS2C02_Mask flag, bool v)
        {
            if (v)
                _ppuMaskReg |= (byte)flag;
            else
                _ppuMaskReg &= (byte)~flag;
        }

        /// <summary>
        /// Helper function to get the state of a specific bit of the status register.
        /// </summary>
        /// <param name="flag">The bit flag to return</param>
        /// <returns>1 or 0</returns>
        public bool GetMaskRegFlag(FLAGS2C02_Mask flag)
        {
            return (byte)(_ppuMaskReg & (byte)flag) > 0 ? true : false;

        }

        /// <summary>
        /// Helper function for setting the bits of the ppu control register.
        /// </summary>
        /// <param name="flag">The flag to set</param>
        /// <param name="v">The value of the bit [0,1]</param>
        private void SetControlFlag(FLAGS2C02_Control flag, bool v)
        {
            if (v)
                _ppuControlReg |= (byte)flag;
            else
                _ppuControlReg &= (byte)~flag;
        }

        /// <summary>
        /// Helper function to get the state of a specific bit of the control register.
        /// </summary>
        /// <param name="flag">The bit flag to return</param>
        /// <returns>1 or 0</returns>
        public bool GetControlFlag(FLAGS2C02_Control flag)
        {
            return (byte)(_ppuControlReg & (byte)flag) > 0 ? true : false;

        }


        #region Fields
        private Cartridge _cartridge;

        /// <summary>
        /// V-Ram used to hold the name table information (2KB).
        /// Split into 2 - 1 KB chunks.
        /// </summary>
        private byte[,] _tblName;
        private byte[,] _tblPattern;
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
            _tblPattern = new byte[2, 4096];
            _palette = new byte[32];

            _ppuMaskReg = (byte)0;
            _ppuStatusReg = (byte)0;
            _ppuControlReg = (byte)0;

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
                    // TODO: Remove DEBUG test
                    SetStatusFlag(FLAGS2C02_Status.VerticalBlank, true);

                    // We only actually need the top 3 bits - the rest are unused.
                    data = (byte)((_ppuStatusReg & 0xE0) | (_ppuStatusReg & 0x1F));

                    // just reading from the status register affects the state of the ppu!
                    // In this case, it resets the status vertical blank flag.
                    SetStatusFlag(FLAGS2C02_Status.VerticalBlank, false);

                    // Also set the address latch back to zero
                    _addressLatch = 0x00;
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

                    // This is a delayed read
                    data = _ppuDataBuffer;
                    _ppuDataBuffer = PPURead(_ppuAddress, true);

                    // Special case for handling palette addresses, as there
                    // is no delay when reading from this memory
                    if (_ppuAddress > 0x3F00)
                        data = _ppuDataBuffer;

                    // The PPU has an auto increment when write to or read from
                    _ppuAddress++;


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
                    _ppuControlReg = data;
                    break;
                case 0x0001: // Mask
                    _ppuMaskReg = data;
                    break;
                case 0x0002: // Status (can't be written too)
                    break;
                case 0x0003: // OAM Address
                    break;
                case 0x0004: // OAM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    if(_addressLatch == 0x00)
                    {
                        // Store the high byte of the ppu address first
                        // _ppuAddress = (ushort)((_ppuAddress & 0x00FF) | (data << 8));
                        _ppuAddress = (ushort)(((data & 0x3F) << 8) | (_ppuAddress & 0x00FF));
                        _addressLatch = 0x01;
                    } else
                    {
                        // then the low byte
                        // _ppuAddress = (ushort)((_ppuAddress & 0xFF00) | data);
                        _ppuAddress = (ushort)((_ppuAddress & 0xFF00) | data);

                        _addressLatch = 0x00;
                    }
                    break;
                case 0x0007: // PPU Data
                    PPUWrite(_ppuAddress, data);

                    // The PPU has an auto increment when write to or read from
                    _ppuAddress++;

                    break;

            }
        }

        public byte PPURead(ushort addr, bool isReadOnly)
        {
            byte data = 0x00;
            addr &= 0x3FFF;

            if(_cartridge.PPURead(addr, out data))
            {
                
            }

            else if(addr >= 0x0000 && addr <= 0x1FFF) // pattern table, CHR Memory
            {
                data = _tblPattern[(addr & 0x1000) >> 12, addr & 0x0FFF];
            }

            else if (addr >= 0x2000 && addr <= 0x3EFF) // Name table
            {

            }

            else if (addr >= 0x3F00 && addr <= 0x3FFF) // palette
            {
                addr &= 0x001F;
                if (addr == 0x0010) addr = 0x0000;
                if (addr == 0x0014) addr = 0x0004;
                if (addr == 0x0018) addr = 0x0008;
                if (addr == 0x001C) addr = 0x000C;
                data = _palette[addr];
            }

            return data;
        }

        /// <summary>
        /// Connects the PPU to its own bus.
        /// </summary>
        public void PPUWrite(ushort addr, byte data)
        {
            addr &= 0x3FFF;

            if (_cartridge.PPUWrite(addr, data))
            {

            }

            else if (addr >= 0x0000 && addr <= 0x1FFF) // pattern table, CHR Memory
            {
                // This is usually a ROM ...
                _tblPattern[(addr & 0x1000) >> 12, addr & 0x0FFF] = data;
            }

            else if (addr >= 0x2000 && addr <= 0x3EFF) // Name table
            {

            }

            else if (addr >= 0x3F00 && addr <= 0x3FFF) // pattern table, CHR Memory
            {
                addr &= 0x001F;
                if (addr == 0x0010) addr = 0x0000;
                if (addr == 0x0014) addr = 0x0004;
                if (addr == 0x0018) addr = 0x0008;
                if (addr == 0x001C) addr = 0x000C;
                _palette[addr] = data;
            }

        }

        public Sprite GetPatternTableSprite(byte patternTableID, byte paletteValue)
        {
            // This function draws the CHR ROM for a given pattern table into
            // a sprite, using a specified palette. Pattern tables consist
            // of 16x16 "tiles or characters". It is independent of the running
            // emulation and using it does not change the systems state, though
            // it gets all the data it needs from the live system.

            // A tile consists of 8x8 pixels. On the NES, pixels are 2 bits, which
            // gives an index into 4 different colours of a specific palette. There
            // are 8 palettes to choose from. Colour "0" in each palette is effectively
            // considered transparent, as those locations in memory "mirror" the global
            // background colour being used. This mechanics of this are shown in 
            // detail in ppuRead() & ppuWrite()

            // Characters on NES
            // ~~~~~~~~~~~~~~~~~
            // The NES stores characters using 2-bit pixels. These are not stored sequentially
            // but in singular bit planes. For example:
            //
            // 2-Bit Pixels       LSB Bit Plane     MSB Bit Plane
            // 0 0 0 0 0 0 0 0	  0 0 0 0 0 0 0 0   0 0 0 0 0 0 0 0
            // 0 1 1 0 0 1 1 0	  0 1 1 0 0 1 1 0   0 0 0 0 0 0 0 0
            // 0 1 2 0 0 2 1 0	  0 1 1 0 0 1 1 0   0 0 1 0 0 1 0 0
            // 0 0 0 0 0 0 0 0 =  0 0 0 0 0 0 0 0 + 0 0 0 0 0 0 0 0
            // 0 1 1 0 0 1 1 0	  0 1 1 0 0 1 1 0   0 0 0 0 0 0 0 0
            // 0 0 1 1 1 1 0 0	  0 0 1 1 1 1 0 0   0 0 0 0 0 0 0 0
            // 0 0 0 2 2 0 0 0	  0 0 0 1 1 0 0 0   0 0 0 1 1 0 0 0
            // 0 0 0 0 0 0 0 0	  0 0 0 0 0 0 0 0   0 0 0 0 0 0 0 0
            //
            // The planes are stored as 8 bytes of LSB, followed by 8 bytes of MSB

            // Each pattern table is 16x16 tiles
            for (ushort tileY = 0; tileY < 16; tileY++)
            {
                for (ushort tileX = 0; tileX < 16; tileX++)
                {
                    // convert 2d coord into 1d coord to access memory.
                    // 256 because each row of tiles is 16 tiles of 16 pixels each.
                    // Therefore, to get to the next y coord for a particular tile, we need to progress by 256 ..
                    ushort offset = (ushort)(tileY * 256 + tileX * 16);

                    // iterate the pixels (of the tile)
                    for (ushort row = 0; row < 8; row++)
                    {
                        byte tile_lsb = PPURead((ushort)(patternTableID * 0x1000 + offset + row + 0x0000), true);
                        byte tile_msb = PPURead((ushort)(patternTableID * 0x1000 + offset + row + 0x0008), true);

                        for (ushort col = 0; col < 8; col++)
                        {
                            // get pixel value [0,3]
                            byte pixel = (byte)(((tile_msb & 0x01) << 1) + (tile_lsb & 0x01));
                            tile_lsb >>= 1;
                            tile_msb >>= 1;

                            // Set the pixels (left <-> right flip)
                            sprPatternTable[patternTableID].SetPixel(
                                tileX * 8 + (7 - col),
                                tileY * 8 + row,
                                GetColorFromPaletteRAM(paletteValue, pixel)
                                );
                        }
                    }
                }
            }

            return sprPatternTable[patternTableID];
        }

        /// <summary>
        /// This is a convenience function that takes a specified palette and pixel
        /// index and returns the appropriate screen colour.
        /// </summary>
        public int GetColorFromPaletteRAM(byte paletteValue, byte pixelValue)
        {
            // "0x3F00"             - Offset into PPU addressable range where palettes are stored
            ushort paletteMemoryOffset = 0x3F00;

            // "paletteValue << 2"  - Each palette is 4 bytes in size
            // "pixelValue"         - Each pixel index is either 0, 1, 2 or 3
            // "& 0x3F"             - Stops us reading beyond the bounds of the palScreen array
            byte paletteCol = PPURead((ushort)(paletteMemoryOffset + (((paletteValue << 2) + pixelValue) & 0x3F)), true);

            return palScreen[paletteCol];
        }
        #endregion
    }
}
