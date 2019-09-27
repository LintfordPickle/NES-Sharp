using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    /// <summary>
    /// The NESCore contains references to all other classes which emulate the NES.
    /// Conceptually, the NESCore is the bus, and is the backbone of the emulation. It handles the reading and writing
    /// to perpherial devices along the main bus.
    /// </summary>
    public class NESCore
    {
        #region Constants
        public const int totalMemoryInBytes = 64 * 1024;
        #endregion

        #region Fields
        // Devices on bus
        private readonly CPU6502 _cpu;
        private readonly PPU2C02 _ppu;
        private Cartridge _cart;
        private byte[] _cpuRam;         // 64KB
        private int _systemClockCounter;

        public SortedList disassembly { get; private set; }
        public bool isDisassemblyLoaded { get; private set; }
        public bool isROMLoaded { get; private set; }
        #endregion

        #region Properties
        /// <summary>
        /// Returns true if there are no more cycles left on the current instruction. This is the delimiter for CPU instructions (which
        /// may or may not take more than one cycle to complete).
        /// </summary>
        public bool IsCycleComplete()
        {
            return _cpu.IsCycleComplete();
        }

        /// <summary>
        /// Returns the number of system cycles which have been run so far
        /// </summary>
        /// <returns></returns>
        public int SystemClockCounter()
        {
            return _systemClockCounter;
        }

        public CPU6502 CPU()
        {
            return _cpu;
        }
        
        public PPU2C02 PPU()
        {
            return _ppu;
        }
        #endregion

        #region Constructor
        public NESCore()
        {
            isROMLoaded = false;

            _cpu = new CPU6502();
            _ppu = new PPU2C02();

            // Connect the Cpu and bus
            _cpu.ConnectBus(this);

            _cpuRam = new byte[totalMemoryInBytes];
            for(int i = 0; i < totalMemoryInBytes; i++)
            {
                _cpuRam[i] = 0x00;
            }

        }
        #endregion

        #region Methods
        public void CPUWrite(ushort addr, byte data)
        {
            // The cartridge gets 'first-dibs' on cpuWrite functions. If the cartrige handles the write, then we can 
            // return, otherwise, pass the write onto the other h/w components.
            // This way, the cartridge can veto what happens on the bus (because read/writes can be ignored).
            if(_cart.CPUWrite(addr, data))
            {
                // The cartridge has handled this write request
            } 

            // Data targeting the RAM
            else if (addr >= 0x0000 && addr <= 0x1FFF)
                _cpuRam[addr % 0x07FF] = data;


            // Data targeting the PPU
            else if (addr >= 0x2000 && addr <= 0x3FFF)
            { 
                _ppu.CPUWrite((ushort)(addr & 0x007), data);
            }

        }

        public byte CPURead(ushort addr, bool memReadonly)
        {
            byte data = 0x00;

            // The cartridge gets 'first-dibs' on cpuRead functions. If the cartrige handles the read, then we can 
            // return, otherwise, pass the write onto the other h/w components.
            // This way, the cartridge can veto what happens on the bus (because read/writes can be ignored).
            if(isROMLoaded && _cart.CPURead(addr, out data))
            {
                // The cartridge has handled this read request
                return data;
            }

            else if (addr >= 0x0000 && addr <= 0x1FFF)
            {
                // 0x07FF as last 6KB of 8KB RAM is mirrored
                data = _cpuRam[addr & 0x07FF];
            }

            // Data targeting the PPU
            else if (addr >= 0x2000 && addr <= 0x3FFF)
            {
                return _ppu.CPURead((ushort)(addr & 0x007), memReadonly);
            }

            return data;
        }
       
        public void InsertCartridge(Cartridge cartridge)
        {
            _cart = cartridge;
            _cart.ConnectBus(this);

            _ppu.ConnectCartridge(cartridge);

            // Notify the bus that we can start accessing ROM for memory read/writes
            isROMLoaded = true;

            if (!isDisassemblyLoaded)
                LoadDisassembly(0x0000, 0xFFFF);

        }

        public void Reset()
        {
            _cpu.Reset();

            _systemClockCounter = 0;

        }

        public void clock()
        {
            _ppu.clock();

            // The 6592 runs 3x slower than the ppu
            if(_systemClockCounter % 3 == 0)
            {
                _cpu.clock();
            }
            _systemClockCounter++;
        }

        /// <summary>
        /// Advances the emulation by  one complete instruction (note: likely multiple clock cycles)
        /// </summary>
        public void StepCPUInstruction()
        {
            do { clock(); } while ( !_cpu.IsCycleComplete() );
            do { clock(); } while (  _cpu.IsCycleComplete());

        }

        /// <summary>
        /// Advances the emulation by one complete PPU frame 
        /// (i.e. completely draw the next frame of the emulation, also advancing the CPU clock multiple times).
        /// </summary>
        public void StepPPUFrame()
        {
            // Clock enough times to draw a single frame
            do { clock(); } while ( !_ppu.frameComplete );

            // Complete the current CPU instruction
            do { clock(); } while (!_cpu.IsCycleComplete());

            // Reset the frame completion flag
            _ppu.frameComplete = true;
        }

        public void LoadDisassembly(ushort start, ushort end)
        {
            disassembly = _cpu.LoadDisassembly(start, end);
            isDisassemblyLoaded = true;

        }
        #endregion
    }
}
