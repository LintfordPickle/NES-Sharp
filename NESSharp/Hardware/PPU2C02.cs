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
        #endregion

        #region Constructor
        public PPU2C02()
        {
            _tblName = new byte[2,1024];
            _palette = new byte[32];

        }
        #endregion

        #region Methods
        public void ConnectCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public void clock()
        {

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
