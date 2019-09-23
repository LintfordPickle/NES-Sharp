using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    public abstract class Mapper
    {
        protected int _NumPRGBanks;
        protected int _NumCHRBanks;

        #region Constructor
        public Mapper(int numPRGBanks, int numCHRBanks)
        {
            _NumPRGBanks = numPRGBanks;
            _NumCHRBanks = numCHRBanks;
        }
        #endregion

        #region Methods
        public abstract bool cpuMapRead(ushort addr, out int mappedAddr);

        public abstract bool cpuMapWrite(ushort addr, out int mappedAddr);

        public abstract bool ppuMapRead(ushort addr, out int mappedAddr);

        public abstract bool ppuMapWrite(ushort addr, out int mappedAddr);
        #endregion

    }
}
