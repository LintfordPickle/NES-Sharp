using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    /// <summary>
    /// Represents a NES cartridge.
    /// The cartridge is conceptually connected to both busses (CPU and PPU)
    /// </summary>
    public class Cartridge
    {
        /// <summary>
        /// iNES Format Header
        /// http://wiki.nesdev.com/w/index.php/INES
        /// </summary>
        struct sHeader
        {
            // NES\n ('$4E', '$45', '$53', '$1A')
            public byte[] name;

            // Size of PRG ROM in 16 KB units
            public byte numPRGRomChunks;

            // Size of CHR ROM in 8 KB units (Value 0 means the board uses CHR RAM)
            public byte numCHRRomChunks;

            // Mapper, mirroring, battery, trainer
            public byte mapper1;

            // Mapper, VS/Playchoice, NES 2.0
            public byte mapper2;

            // PRG-RAM size (a rarely used extension)
            public byte extPRGRamSize;

            // TV system (a rarely used extension)
            public byte extTVSystem1;

            // TV system, PRG-RAM presence(unofficial, rarely used extension)
            public byte extTVSystem2;

            // Unused padding present within ROM
            public byte[] unused;
        };

        #region Fields
        private NESCore _bus;

        // Because we don#t know how big the PRG and CHR memory on the cartridge needs to be until we have read the file (due to the mapper
        // and bank switching), define the PRG and CHR memory as a list of bytes.
        private byte[] _PRGMemory;
        private byte[] _CHRMemory;

        private int _mapperID; // which mapper are we using
        private int _prgBanks; // how many prg banks are there
        private int _chrBanks; // how many chr banks are there

        private sHeader _fileHeader;

        private Mapper _mapper;

        public string fileName { get; private set; }
        #endregion

        #region Constructor
        /// <remarks>
        /// For hardware information about particular ROMs: http://bootgod.dyndns.org:7777
        /// </remarks>
        /// <param name="fileName"></param>
        public Cartridge(String fileName)
        {
            this.fileName = fileName;

            _mapperID = 0;
            _prgBanks = 0;
            _chrBanks = 0;

            // open file as binary file
            var fs = new FileStream(fileName, FileMode.Open);
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read the file header
                _fileHeader.name = reader.ReadBytes(4);
                _fileHeader.numPRGRomChunks = reader.ReadByte();
                _fileHeader.numCHRRomChunks = reader.ReadByte();
                _fileHeader.mapper1 = reader.ReadByte();
                _fileHeader.mapper2 = reader.ReadByte();
                _fileHeader.extPRGRamSize = reader.ReadByte();
                _fileHeader.extTVSystem1 = reader.ReadByte();
                _fileHeader.extTVSystem2 = reader.ReadByte();
                _fileHeader.unused = reader.ReadBytes(5);

                // Trainer information occupies the next 512 bytes, if it is available on the ROM.
                if((_fileHeader.mapper1 & 0x04) == 0x04)
                {
                    reader.ReadBytes(512);
                }

                // Extract which mapper the rom is using
                // Mapper 1 lsb contains lower nibble of mapper number
                // Mapper 2 msb contains upper nibble of mapper number (shifting used to erase lower nibble)
                _mapperID = ((_fileHeader.mapper2 >> 4) << 4) | (_fileHeader.mapper1 >> 4);

                if(_mapperID == 0)
                {
                    _mapper = new Mappers.Mapper_000(_fileHeader.numPRGRomChunks, _fileHeader.numCHRRomChunks);
                }
                else
                {
                    // placeholder for all other mappers ...
                    throw new NotImplementedException($"NES Mapper {_mapperID} not implemented. This ROM will not work");
                }

                // Need to figure out which version of the iNES file it is we are loading
                byte fileType = 1;
                if(fileType == 0)
                {
                    // placeholder
                }

                if (fileType == 1)
                {
                    // Read how many banks of data are in the rom for the program memory and read that into _PRGMemory
                    // File Format specifies a single bank of PRG memory as 16KB / 16384 Bytes
                    _prgBanks = _fileHeader.numPRGRomChunks;
                    // _PRGMemory = new byte[_prgBanks * 16384];
                    _PRGMemory = reader.ReadBytes(_prgBanks * 16384);

                    // Read how many banks of data are in the rom for the character memory and read that into _CHRMemory
                    // File Format specifies a single bank of PRG memory as 8KB / 8192 Bytes
                    _chrBanks = _fileHeader.numCHRRomChunks;
                    // _CHRMemory = new byte[_chrBanks * 8192];
                    _CHRMemory = reader.ReadBytes(_chrBanks * 8192);

                }

                if (fileType == 2)
                {
                    // place holder
                }


                reader.Close();

            }

        }
        #endregion

        #region Methods
        public void ConnectBus(NESCore n)
        {
            _bus = n;
        }

        public bool CPURead(ushort addr, out byte data)
        {
            int mapped_addr = addr;

            // Check if the cartridge needs to handle this cpu Write command or not
            if (_mapper.cpuMapRead(addr, out mapped_addr))
            {
                // If true, then the cartridge will handle this read request using the newly mapper
                // address, mapped by the mapper.
                data = _PRGMemory[mapped_addr];
                return true;
            }

            // otherwise, return false
            data = 0x00;
            return false;
        }

        public bool CPUWrite(ushort addr, byte data)
        {
            int mapped_addr = addr;

            // Check if the cartridge needs to handle this cpu Write command or not
            if(_mapper.cpuMapWrite(addr, out mapped_addr))
            {
                // In this case, the data is written to the cartridge
                _PRGMemory[mapped_addr] = data;
                return true;
            }

            return false;
        }

        public bool PPURead(ushort addr, out byte data)
        {
            int mapped_addr = addr;

            // Check if the cartridge needs to handle this cpu Write command or not
            if (_mapper.ppuMapRead(addr, out mapped_addr))
            {
                // If true, then the cartridge will handle this read request using the newly mapper
                // address, mapped by the mapper.
                data = _CHRMemory[mapped_addr];
                return true;
            }

            // otherwise, return false
            data = 0x00;
            return false;
        }

        public bool PPUWrite(ushort addr, byte data)
        {
            // CHR memory is ROM
            return false;
        }
        #endregion

    }
}
