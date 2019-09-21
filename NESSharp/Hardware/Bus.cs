using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    public class Bus
    {
        #region Consts
        public const int TOTAL_MEMORY_BYTES = 64 * 1024;
        #endregion

        #region Variables
        // Devices on bus

        public byte[] ram;         // 64KB
        #endregion

        #region Constructor
        public Bus()
        {
            ram = new byte[TOTAL_MEMORY_BYTES];
            for(int i = 0; i < TOTAL_MEMORY_BYTES; i++)
            {
                ram[i] = 0x00;
            }

        }
        #endregion

        #region Methods
        public void Write(ushort addr, byte data)
        {
            if(addr >= 0x0000 && addr <= 0xFFFF)
                ram[addr] = data;
        }

        public byte Read(ushort addr, bool memReadonly)
        {
            if (addr >= 0x0000 && addr <= 0xFFFF)
                return ram[addr];

            return 0x00;
        }
        #endregion

    }
}
