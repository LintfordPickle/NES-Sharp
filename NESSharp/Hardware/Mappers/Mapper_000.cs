using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware.Mappers
{
    public class Mapper_000 : Mapper
    {
        #region Constructor
        public Mapper_000(int numPRGBanks, int numCHRBanks) : base(numPRGBanks, numCHRBanks)
        {

        }
        #endregion

        #region Methods
        public override bool cpuMapRead(ushort addr, out int mappedAddr)
        {
            // The mapper need only react to memory ranges it cares about
            // in this case, that means the PRG Memory range
            if(addr >= 0x8000 && addr <= 0xFFFF)
            {
                // if PRGROM is 16KB
                //     CPU Address Bus          PRG ROM
                //     0x8000 -> 0xBFFF: Map    0x0000 -> 0x3FFF
                //     0xC000 -> 0xFFFF: Mirror 0x0000 -> 0x3FFF
                // if PRGROM is 32KB
                //     CPU Address Bus          PRG ROM
                //     0x8000 -> 0xFFFF: Map    0x0000 -> 0x7FFF	

                // For mapper 000, depending on how many banks are in the ROM, we need to mirror the first 16KB
                mappedAddr = addr & (ushort)(_NumPRGBanks > 1 ? 0x7FFF : 0x3FFF);

                return true;
            }

            mappedAddr = addr;

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out int mappedAddr)
        {
            // The mapper need only react to memory ranges it cares about
            // in this case, that means the PRG Memory range
            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                // For mapper 000, depending on how many banks are in the ROM, we need to mirror the first 16KB
                mappedAddr = addr & (ushort)(_NumPRGBanks > 1 ? 0x7FFF : 0x3FFF);

                return true;
            }

            mappedAddr = addr;
            return false;
        }

        public override bool ppuMapRead(ushort addr, out int mappedAddr)
        {
            // Mapper 0 ppu has nothing to do
            mappedAddr = addr;

            // Pattern tables
            if (addr >= 0x0000 && addr <= 0x1FFF)
            {    
                return true;
            }

            return false;
        }

        public override bool ppuMapWrite(ushort addr, out int mappedAddr)
        {
            // CHR ROM is read-only
            mappedAddr = addr;
            return false;
        }
        #endregion
    }
}
