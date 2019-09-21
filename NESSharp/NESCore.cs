using NESSharp.Hardware;
using System;

namespace NESSharp
{
    public class NESCore
    {
        #region Variables
        public Bus NESBus { get; private set; }
        public CPU6502 NESCPU { get; private set; }
        #endregion

        #region Constructor
        public NESCore()
        {
            NESCPU = new CPU6502();
            NESBus = new Bus();

            // Connect the Cpu and bus
            NESCPU.ConnectBus(NESBus);

            Reset();
        }
        #endregion

        #region Methods
        public void Reset()
        {
            // Set the reset vector to the start of program memory
            NESBus.ram[0xFFFC] = 0x00;
            NESBus.ram[0xFFFD] = 0x80;

            NESCPU.Reset();
            NESCPU.stepOneInstruction();
        }

        public void LoadROMFromFile(String filename, bool loadDisassembly)
        {
            throw new NotImplementedException();
        }

        public void LoadROMFromString(String romByteString, bool loadDisassembly)
        {
            NESCPU.LoadROM(romByteString);
            if(loadDisassembly)
                NESCPU.LoadDisassembly(0x0000, 0xFFFF);
        }
        #endregion

    }
}
