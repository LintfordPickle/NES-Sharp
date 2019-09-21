using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESSharp.Hardware
{
    // Reference material:
    // General Overview:    https://wiki.nesdev.com
    // Datasheet:           http://archive.6502.org/datasheets/rockwell_r650x_r651x.pdf
    // OpCode descriptions: https://www.masswerk.at/6502/6502_instruction_set.html

    public class CPU6502
    {
        #region Constants, Strcuts and Enums
        public enum FLAGS6502
        {
            C = (1 << 0),     // Carry bit
            Z = (1 << 1),     // Zero
            I = (1 << 2),     // Disable Interupts
            D = (1 << 3),     // Decimal Mode (not used in 2A03/NES)
            B = (1 << 4),     // Break
            U = (1 << 5),     // Unused
            V = (1 << 6),     // Overflow
            N = (1 << 7),     // Negative
        }

        public struct Instruction
        {
            public string name { get; private set; }         // mnemonic for disassembler
            public Func<byte> operate { get; private set; }  // pointer to opcode function (see above)
            public Func<byte> addrmode { get; private set; } // pointer to addrmode function (see above)
            public int cycles { get; private set; }          // number of cycles needed to complete

            public Instruction(string name, Func<byte> opcodeFunction, Func<byte> addrmode, int cycles) : this()
            {
                this.name = name;
                this.operate = opcodeFunction;
                this.addrmode = addrmode;
                this.cycles = cycles;
            }
        }
        #endregion

        #region Variables
        private Bus bus;

        /// <summary>
        /// The lookup table contains information about each of the Instructions on the 6502.
        /// The list can be indexed by the OpCode hex value
        /// </summary>
        private List<Instruction> lookup;

        public SortedList Disassembly { get; private set; }
        public bool DisassemblyLoaded { get; private set; }

        public byte status { get; private set; }   // Status register
        public byte a { get; private set; }        // Accumalator
        public byte x { get; private set; }        // X Register
        public byte y { get; private set; }        // Y Register
        public byte stkp { get; private set; }     // Stack Pointer
        public ushort pc { get; private set; }    // program counter
        public byte opcode { get; private set; }  // Current opcode

        // depending on the addressing mode, we'll need to read from different locations in memory.
        private ushort addr_abs = 0x0000;
        private ushort addr_rel = 0x0000;
        private byte fetched = 0x00;  // working input value to ALU
        private int cycles;
        #endregion

        #region Address Mode Functions
        /// <summary>
        /// Accumulator Addressing [Accum].
        /// This form of addressing represented with a one byte instruction, implying an operation
        /// of the accumulator.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte ACC()
        {
            fetched = a;
            return 0x00;
        }

        /// <summary>
        /// Implied Addressing Mode.
        /// In the implied addressing mode, the address containing the operand is implicitly stated
        /// in the operation code of the instruction.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte IMP()
        {
            fetched = a;
            return 0x00;
        }

        /// <summary>
        /// Immediate Addressing Mode.
        /// In immediate addressing mode, the second byte of the instruction contains the operand,
        /// with no further memory addressing required.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte IMM()
        {
            addr_abs = pc;
            pc++;
            return 0x00;
        }

        /// <summary>
        /// Zero Page Addressing Mode.
        /// The zero page instructions allow for shorter code and execution times by fetching only the 
        /// second byte of the instruction and assuming a zero high address byte. Careful use of the 
        /// zero page can result in significant increase in code efficiency.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte ZP0()
        {
            addr_abs = Read(pc);
            pc++;
            addr_abs &= 0x00FF;
            return 0x00;
        }

        /// <summary>
        /// Zero page addressing with X register offset.
        /// This form of addressing is used with the index register and is referred to as
        /// "Zero Page X". The effective address is calculated by adding the second byte to 
        /// the contents of the index register. Since this is a form of Zero Page addressing,
        /// the content of the second byte references a location in page zero.
        /// Additionally, due to the "Zero Page" addressing nature of this mode, no carry is
        /// added to the high order eight bits of memory and crossing of page boundaries does
        /// not occur.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte ZPX()
        {
            addr_abs = (ushort)(Read(pc) + x);
            pc++;
            addr_abs &= 0x00FF;

            return 0x00;
        }

        /// <summary>
        /// Zero page addressing with Y register offset.
        /// This form of addressing is used with the index register and is referred to as
        /// "Zero Page Y". The effective address is calculated by adding the second byte to 
        /// the contents of the index register. Since this is a form of Zero Page addressing,
        /// the content of the second byte references a location in page zero.
        /// Additionally, due to the "Zero Page" addressing nature of this mode, no carry is
        /// added to the high order eight bits of memory and crossing of page boundaries does
        /// not occur.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte ZPY()
        {
            addr_abs = (ushort)(Read(pc) + y);
            pc++;
            addr_abs &= 0x00FF;

            return 0x00;
        }

        /// <summary>
        /// Absolute Addressing.
        /// In absolute addressing, the second byte of the instruction specifies the eight low order 
        /// bits of the effective address while the thrid byte specifies the eight high order bits. 
        /// Thus, the absolute addressing mode allows access to the entire 64K bytes of addressable 
        /// memory.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns> 
        byte ABS()
        {
            byte loByte = Read(pc);
            pc++;
            byte hiByte = Read(pc);
            pc++;

            addr_abs = (ushort)((hiByte << 8) | loByte);

            return 0x00;
        }

        /// <summary>
        /// Absolute Addressind Mode Indexed X.
        /// This form of addressing is used in conjunction with the X index register and is
        /// referred to as "Absolute. X". The effective address is formed by adding the contents
        /// of X to the address contained in the second and third bytes of the instruction. This
        /// mode allows the index register to contain the index or count value and the instruction
        /// to contain the base address. This type of indexing allows referencing of any location
        /// and the index may modify multiple fields, resulting in reduced coding and execution time.
        /// </summary>
        /// <returns>Number of additional cycles required (due to crossing page boundaries)</returns>
        byte ABX()
        {
            byte loByte = Read(pc);
            pc++;
            byte hiByte = Read(pc);
            pc++;

            addr_abs = (ushort)((hiByte << 8) | loByte);
            addr_abs += x;

            // If after offseting with the X register, if the address has changed to a different page,
            // (stored in the high byte), then we need to indicate an increase in cycles needed.
            if ((addr_abs & 0x00FF) != (hiByte << 8))
                return 0x01;
            else
                return 0x00;
        }

        /// <summary>
        /// Absolute Addressind Mode Indexed Y.
        /// This form of addressing is used in conjunction with the Y index register and is
        /// referred to as "Absolute. Y". The effective address is formed by adding the contents
        /// of X to the address contained in the second and third bytes of the instruction. This
        /// mode allows the index register to contain the index or count value and the instruction
        /// to contain the base address. This type of indexing allows referencing of any location
        /// and the index may modify multiple fields, resulting in reduced coding and execution time.
        /// </summary>
        /// <returns>Number of additional cycles required (due to crossing page boundaries)</returns>
        byte ABY()
        {
            byte loByte = Read(pc);
            pc++;
            byte hiByte = Read(pc);
            pc++;

            addr_abs = (ushort)((hiByte << 8) | loByte);
            addr_abs += y;

            // If after offseting with the Y register, if the address has changed to a different page,
            // (stored in the high byte), then we need to indicate an increase in cycles needed.
            if ((addr_abs & 0x00FF) != (hiByte << 8))
                return 0x01;
            else
                return 0x00;

        }

        /// <summary>
        /// Relative Addressing Mode.
        /// Relative addressing mode is only used with branching instructions and establishes 
        /// a destination for the conditional branch.
        /// The second byte of the instruction is an operand. This operand is an offset which is
        /// added to the program counter when the counter is set at the next instruction.
        /// The reange of the osset is -128 to +127 bytes.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte REL()
        {
            addr_rel = Read(pc);
            pc++;

            // Branching instructions cannot jump further than 127 memory locations. - maintain the sign
            if ((addr_rel & 0x80) == 0x80)
                addr_rel |= 0xFF00;

            return 0x00;
        }

        /// <summary>
        /// Absolute Indirect Addressing Mode [indirect].
        /// The second byte of the instruction contains the low order byte of a memory location.
        /// The high order eight bits of that memory location are contained in the third byte
        /// of the instruction. The contents of the fully specified memory location are the low
        /// order byte of the effective address. The next memory location contains the high 
        /// order byte of the effective address which is loaded into the sixteen bits of the 
        /// program counter (JMP [IND] only).
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte IND()
        {
            byte ptrLoByte = Read(pc);
            pc++;
            byte ptrHiByte = Read(pc);
            pc++;

            ushort ptrToMemoryAddr = (ushort)((ptrHiByte << 8) | ptrLoByte);

            if (ptrLoByte == 0x00FF) // Simulate page boundary hardware bug
            {
                addr_abs = (ushort)((Read((ushort)(ptrToMemoryAddr + 0xFF00)) << 8) | Read(ptrToMemoryAddr));
            }
            else
            {
                addr_abs = (ushort)((Read((ushort)(ptrToMemoryAddr + 1)) << 8) | Read(ptrToMemoryAddr));
            }


            return 0x00;
        }

        /// <summary>
        /// Indexed Indirect Addressing [(IND, X)].
        /// In indexed indirect addressing, the second byte of the instruction is added to the 
        /// contents of the index register X, discarding the carry.
        /// The result of this addition points to a memory location on page zero which contains
        /// the low order byte of the effective address. The next memory location in page zero
        /// contains the higher order byte of the effective address. Both memory locations
        /// specifying the effective address must be in zero page.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte IZX()
        {
            ushort t = Read(pc);
            pc++;

            byte ptrLoByte = Read((ushort)((ushort)(t + x) & 0x00FF));
            byte ptrHiByte = Read((ushort)((ushort)(t + x + 1) & 0x00FF));
            
            addr_abs = (ushort)((ptrHiByte << 8) | ptrLoByte);

            return 0x00;
        }

        /// <summary>
        /// In indirect indexed addressing, the second byte of the instruction points to a 
        /// memory location in page zero. The contents of this memory location are added to 
        /// the contents of index register Y. The result is the lower order byte of the 
        /// effective address. The carry from this addition is added to the contents of the next
        /// page zero memory location to form the high order byte of the effective address.
        /// </summary>
        /// <returns>Number of additional cycles needed, if any</returns>
        byte IZY()
        {
            ushort t = Read(pc);
            pc++;

            byte ptrLoByte = Read((ushort)((t + 0) & 0x00FF));
            byte ptrHiByte = Read((ushort)((t + 1) & 0x00FF));

            addr_abs = (ushort)((ptrHiByte << 8) | ptrLoByte);
            addr_abs += y;

            // If we cross a page boundary, we need to process an extra cycle
            if ((addr_abs & 0xFF00) != (ptrHiByte << 8))
                return 0x01;
            else 
                return 0x00;

        }
        #endregion

        #region OpCode Functions
        /// <summary>
        /// Add Memory to Accumulator with Carry.
        /// Function:  A + M + C -> A, C 
        /// Flags Out: N, Z, C, Y
        /// </summary>
        byte ADC()
        {
            fetch();

            // perform addition in 16-bit domain
            ushort temp = (ushort)((ushort)a + (ushort)fetched + (ushort)((GetFlag(FLAGS6502.C) ? 1 : 0)));

            SetFlag(FLAGS6502.C,  temp > 255);           // Set carry flag
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);  // Set zero flag
            SetFlag(FLAGS6502.N, (temp & 0x80) == 0x80); // Set the overflow flag

            SetFlag(FLAGS6502.V, ((~(a ^ fetched) & (a ^ temp)) & 0x0080) == 0x0080);

            /**
             * Truth table for working out whether to set the oVerflow flag
             * 
             * A    M    R  |  V   A^R A^M ~(A^M)
             * 0    0    0  |  0   0   0   1
             * 0    0    1  |  1   1   0   1       // We need to set V-Flag in this case
             * 0    1    0  |  0   0   1   0
             * 0    1    1  |  0   1   1   0
             * 1    0    0  |  0   1   1   0
             * 1    0    1  |  0   0   1   0
             * 1    1    0  |  1   1   0   1        // We need to set V-Flag in this case
             * 1    1    1  |  0   0   0   1
             * 
             * V = (A^R) & ~(A^M)
             * 
             */

            // finally store the result back in the accumulator (8-bit)
            a = (byte)(temp & 0x00FF);

            return 1;
        }

        /// <summary>
        /// AND Memory with Accumulator.
        /// Function:  A AND M -> A
        /// Flags Out: N, Z
        /// </summary>
        byte AND()
        {
            fetch();
            a = (byte)(a & fetched);

            // Update the status register
            SetFlag(FLAGS6502.Z, a == 0x00); // zero
            SetFlag(FLAGS6502.N, a == 0x80); // neg

            // Return whether or not this instruction is a *candidate* for additional cycles
            return 0x01;
        }

        /// <summary>
        /// Shift Left One Bit (Memory or Accumulator)
        /// Function:  C <- [76543210] <- 0
        /// Flags Out: N, Z, C
        /// </summary>
        byte ASL()
        {
            fetch();

            // 16-bit domain
            ushort temp = (byte)(fetched << 1);

            SetFlag(FLAGS6502.C, (ushort)(temp & 0xFF00) > 0);  // carry
            SetFlag(FLAGS6502.Z, (ushort)(temp & 0x00FF) == 0); // zero
            SetFlag(FLAGS6502.N, temp == 0x80); // neg

            if (lookup[opcode].addrmode == IMP)
                a = (byte)(temp & 0x00FF);
            else
                Write(addr_abs, (byte)(temp & 0x00FF));

            return 0x00;
        }

        /// <summary>
        /// BCC (Branch on Carry Clear)
        /// Function:  branch on C = 0
        /// Flags Out: 
        /// </summary>
        byte BCC()
        {
            if (!GetFlag(FLAGS6502.C))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            return 0x00;
        }

        /// <summary>
        /// BCS (Branch on Carry Set)
        /// Function:  branch on C = 1
        /// Flags Out:
        /// </summary>
        byte BCS()
        {
            if (GetFlag(FLAGS6502.C))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            return 0x00;
        }

        /// <summary>
        /// BEQ (Branch on Result Zero)
        /// Function:  branch on Z = 1
        /// Flasg Out: 
        /// </summary>
        byte BEQ()
        {
            if (GetFlag(FLAGS6502.Z))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            // extra cycles are handled above
            return 0x00;
        }

        /// <summary>
        /// Test Bits in Memory with Accumulator
        /// bits 7 and 6 of operand are transfered to bit 7 and 6 of SR (N,V);
        /// the zeroflag is set to the result of operand AND accumulator.
        /// Function:  A AND M, M7 -> N, M6 -> V
        /// Flags Out: N (M7), Z, V (M6)
        /// </summary>
        byte BIT()
        {
            fetch();

            byte temp = (byte)(a & fetched);

            SetFlag(FLAGS6502.N, (byte)(fetched & 0xFF) == 0x40);
            SetFlag(FLAGS6502.V, (byte)(fetched & 0xFF) == 0x20);

            SetFlag(FLAGS6502.Z, temp == 0x00);

            return 0x00;
        }

        /// <summary>
        /// Branch on Result Minus
        /// Function:  branch on N = 1
        /// Flags Out: 
        /// </summary>
        byte BMI()
        {
            if (GetFlag(FLAGS6502.N))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            // extra cycles are handled above
            return 0x00;
        }

        /// <summary>
        /// Branch on Result not Zero
        /// Function:  branch on Z = 0
        /// Flags Out: 
        /// </summary>
        byte BNE()
        {
            if (!GetFlag(FLAGS6502.Z))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            // extra cycles are handled above
            return 0x00;
        }

        /// <summary>
        /// Branch on Result Plus
        /// Function:  branch on N = 0
        /// Flags Out: 
        /// </summary>
        byte BPL()
        {
            if (!GetFlag(FLAGS6502.N))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            return 0x00;
        }

        /// <summary>
        /// Force Break
        /// Function:  interrupt, push PC+1(2??), push SR 
        /// Flags Out: I
        /// n.b. There seems to be some conflict between reference material about
        /// how many bytes the Force Break consumes, either 1 or 2. This code
        /// is based on https://github.com/OneLoneCoder/olcNES/blob/master/Part%232%20-%20CPU/olc6502.cpp
        /// </summary>
        byte BRK()
        {
            pc++;

            SetFlag(FLAGS6502.I, true);
            Write((ushort)(0x0100 + stkp), (byte)((pc >> 8) & 0x00FF));
            stkp--;
            Write((ushort)(0x0100 + stkp), (byte)(pc & 0x00FF));
            stkp--;

            // SetFlag(FLAGS6502.B, true);
            Write((ushort)(0x0100 + stkp), status);
            stkp--;
            // SetFlag(FLAGS6502.B, false);

            pc = (ushort)(Read(0xFFFE) | ((Read(0xFFFF) << 8)));

            return 0x00;
        }

        /// <summary>
        /// Branch on Overflow Clear
        /// Function:  branch on V = 0 
        /// Flags Out: 
        /// </summary>
        byte BVC()
        {
            if (!GetFlag(FLAGS6502.V))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            // extra cycles are handled above
            return 0x00;
        }

        /// <summary>
        /// Branch on Overflow Set
        /// Function:  branch on V = 1 
        /// Flags Out: 
        /// </summary>
        byte BVS()
        {
            if (GetFlag(FLAGS6502.V))
            {
                // Add 1 additional cycle if branch occurs to the same page
                // Add 2 additional cycle if branch occurs to different page

                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                // if we crossed a page boundary, then increase the cycle count
                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;

            }

            return 0x00;
        }

        /// <summary>
        /// Clear Carry Flag
        /// Function:  0 -> C 
        /// Flags Out: C
        /// </summary>
        byte CLC()
        {
            SetFlag(FLAGS6502.C, false);
            return 0x00;
        }

        /// <summary>
        /// Clear Decimal Mode
        /// Function:  0 -> D
        /// Flags Out: D
        /// </summary>
        byte CLD()
        {
            SetFlag(FLAGS6502.D, false);
            return 0x00;
        }

        /// <summary>
        /// Clear Interrupt Disable Bit
        /// Function:  0 -> I
        /// Flags Out: I
        /// </summary>
        byte CLI()
        {
            SetFlag(FLAGS6502.I, false);
            return 0x00;
        }

        /// <summary>
        /// Clear Overflow Flag
        /// Function:  0 -> V
        /// Flags Out: V
        /// </summary>
        byte CLV()
        {
            SetFlag(FLAGS6502.V, false);
            return 0x00;
        }

        /// <summary>
        /// Compare Memory with Accumulator
        /// Function:  A - M
        /// Flags Out: N, Z, C
        /// </summary>
        byte CMP()
        {
            fetch();

            ushort temp = (ushort)(a - fetched);

            SetFlag(FLAGS6502.N, (ushort)(temp & 0x8000) == 0x8000);
            SetFlag(FLAGS6502.Z, (byte)(temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.C, a >= fetched);

            return 0x01;
        }

        /// <summary>
        /// Compare Memory and Index X
        /// Function:  X - M
        /// Flags Out: N, Z, C
        /// </summary>
        byte CPX()
        {
            fetch();

            ushort temp = (ushort)(x - fetched);

            SetFlag(FLAGS6502.N, (ushort)(temp & 0x8000) == 0x8000);
            SetFlag(FLAGS6502.Z, (byte)(temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.C, x >= fetched);

            return 0x00;
        }

        /// <summary>
        /// Compare Memory and Index Y
        /// Function:  Y - M
        /// Flags Out: N, Z, C
        /// </summary>
        byte CPY()
        {
            fetch();

            ushort temp = (ushort)(y - fetched);

            SetFlag(FLAGS6502.N, (ushort)(temp & 0x8000) == 0x8000);
            SetFlag(FLAGS6502.Z, (byte)(temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.C, y >= fetched);

            return 0x00;
        }

        /// <summary>
        /// Decrement Memory by One
        /// Function:  M - 1 -> M
        /// Flags Out: N, Z
        /// </summary>
        byte DEC()
        {
            fetch();

            ushort temp = fetched--;

            SetFlag(FLAGS6502.Z, fetched == 0x00);
            SetFlag(FLAGS6502.N, (byte)(fetched & 0x80) == 0x80);

            Write(addr_abs, (byte)(temp & 0x00FF));

            return 0x00;
        }

        /// <summary>
        /// Decrement Index X by One
        /// Function:  X - 1 -> X
        /// Flags Out: N, Z
        /// </summary>
        byte DEX()
        {
            x--;

            SetFlag(FLAGS6502.Z, x == 0x00);
            SetFlag(FLAGS6502.N, (byte)(x & 0x80) == 0x80);
            
            return 0x00;
        }

        /// <summary>
        /// Decrement Index Y by One
        /// Function:  Y - 1 -> Y
        /// Flags Out: N, Z
        /// </summary>
        byte DEY()
        {
            y--;

            SetFlag(FLAGS6502.Z, y == 0x00);
            SetFlag(FLAGS6502.N, (byte)(y & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Exclusive-OR Memory with Accumulator
        /// Function:  A EOR M -> A
        /// Flags Out: N, Z
        /// </summary>
        byte EOR()
        {
            fetch();

            a ^= fetched;

            SetFlag(FLAGS6502.Z, a == 0x00);
            SetFlag(FLAGS6502.N, (byte)(a & 0x80) == 0x80);

            return 0x01;
        }

        /// <summary>
        /// Increment Memory by One
        /// Function:  M + 1 -> M
        /// Flags Out: N, Z
        /// </summary>
        /// <returns></returns>
        byte INC()
        {
            fetch();

            ushort temp = (ushort)(fetched + 1);

            SetFlag(FLAGS6502.Z, (byte)(temp & 0x00FF) == 0x00);
            SetFlag(FLAGS6502.N, (byte)(temp & 0x0080) == 0x0080);

            return 0x00;
        }

        /// <summary>
        /// Increment Index X by One
        /// Function:  X + 1 -> X
        /// Flags Out: N, Z
        /// </summary>
        byte INX()
        {
            x++;

            SetFlag(FLAGS6502.Z, x == 0x00);
            SetFlag(FLAGS6502.N, (byte)(x & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Increment Index Y by One
        /// Function:  Y + 1 -> Y
        /// Flags Out: N, Z
        /// </summary>
        byte INY()
        {
            y++;

            SetFlag(FLAGS6502.Z, y == 0x00);
            SetFlag(FLAGS6502.N, (byte)(y & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Jump to New Location
        /// Function:  (PC+1) -> PCL
        ///            (PC+2) -> PCH
        /// Flags Out: 
        /// </summary>
        byte JMP()
        {
            pc = addr_abs;
            return 0x00;
        }

        /// <summary>
        /// Jump to New Location Saving Return Address
        /// Function:  (PC+1) -> PCL
        ///            (PC+2) -> PCH
        /// Flags Out:
        /// </summary>
        byte JSR()
        {
            // Write current 16-bit pc to the stack
            Write((ushort)(0x0100 + stkp), (byte)((pc >> 8) & 0x00FF));
            stkp--;
            Write((ushort)(0x0100 + stkp), (byte)(pc & 0x00FF));
            stkp--;

            pc = addr_abs;

            return 0x00;
        }

        /// <summary>
        /// Load Accumulator with Memory
        /// Function:  M -> A 
        /// Flags Out: N, Z
        /// </summary>
        byte LDA()
        {
            fetch();

            a = fetched;
            SetFlag(FLAGS6502.Z, (a & 0x00) == 0x00);
            SetFlag(FLAGS6502.N, (a & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Load Index X with Memory.
        /// Function:  M -> X 
        /// Flags Out: N, Z
        /// </summary>
        byte LDX()
        {
            fetch();

            x = fetched;
            SetFlag(FLAGS6502.Z, x == 0x00);
            SetFlag(FLAGS6502.N, (x & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Load Index Y with Memory.
        /// Function:  M -> Y  
        /// Flags Out:
        /// </summary>
        byte LDY()
        {
            fetch();

            y = fetched;
            SetFlag(FLAGS6502.Z, y == 0x00);
            SetFlag(FLAGS6502.N, (y & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Shift One Bit Right (M or A)
        /// </summary>
        byte LSR()
        {
            fetch();
            SetFlag(FLAGS6502.C, (fetched & 0x0001) == 0x0001);

            // Shift right 1 bit
            byte temp = (byte)(fetched >> 1);

            SetFlag(FLAGS6502.Z, (fetched & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.N, (fetched & 0x00FF) == 0x0080);

            // if memory or acc can be determined based on the address mode
            if (lookup[opcode].addrmode == IMP)
                a = (byte)(temp & 0x00FF);
            else
                Write(addr_abs, (byte)(temp & 0x00FF));

            return 0x00;
        }

        /// <summary>
        /// No Operation (cycles 2)
        /// Function:  ---
        /// </summary>
        byte NOP() { return 0x00; }

        /// <summary>
        /// OR with accumulator
        /// Function:  A OR M -> A
        /// Flags Out: N, Z
        /// </summary>
        byte ORA()
        {
            fetch();

            a |= fetched;

            SetFlag(FLAGS6502.N, (byte)(a & 0x80) == 0x80);
            SetFlag(FLAGS6502.Z, a == 0x00);

            return 0x01;
        }

        /// <summary>
        /// Push accumulator on stack
        /// Function:  push A
        /// Flags Out: -
        /// </summary>
        byte PHA()
        {
            // The stack memory begins at 0x0100
            Write((ushort)(0x0100 + stkp), a);
            stkp--;

            return 0x00;
        }

        /// <summary>
        /// Push Processor Status on Stack
        /// Function:  push SR
        /// Flags Out: -
        /// </summary>
        byte PHP()
        {
            // The stack memory begins at 0x0100
            Write((ushort)(0x0100 + stkp), status);
            stkp--;

            return 0x00;
        }

        /// <summary>
        /// Pull accumulator from stack
        /// Function:  pull A
        /// Flags Out: N, Z
        /// </summary>
        byte PLA()
        {
            // The stack memory begins at 0x0100           
            a = Read((ushort)(0x0100 + stkp));
            stkp++;

            SetFlag(FLAGS6502.Z, a == 0x00); // Set zero flag
            SetFlag(FLAGS6502.N, (a & 0x80) == 0x80); // Set negative flag
            
            return 0x00;

        }

        /// <summary>
        /// Pull Processor Status from Stack
        /// Function:  pull SR
        /// Flags Out: from stack
        /// </summary>
        byte PLP()
        {
            // The stack memory begins at 0x0100           
            status = Read((ushort)(0x0100 + stkp));
            stkp++;

            return 0x00;

        }

        /// <summary>
        /// Rotate One Bit Left (Memory or Accumulator)
        /// Function:  C <- [76543210] <- C
        /// Flags Out: N, Z, C
        /// </summary>
        byte ROL()
        {
            fetch();

            byte c = (byte)(GetFlag(FLAGS6502.C) ? 0x01 : 0x00);
            ushort temp = (ushort)((fetched << 1) | c);

            SetFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.N, (temp & 0x0080) == 0x0080);
            if (lookup[opcode].addrmode == IMP)
                a = (byte)(temp & 0x00FF);
            else
                Write(addr_abs, (byte)(temp & 0x00FF));

            return 0x00;
        }

        /// <summary>
        /// Rotate One Bit Right (Memory or Accumulator)
        /// Function:  C -> [76543210] -> C
        /// Flags Out: N, Z, C
        /// </summary>
        byte ROR()
        {
            fetch();

            byte c = (byte)(GetFlag(FLAGS6502.C) ? 0x01 : 0x00);
            ushort temp = (ushort)((c << 7) | (fetched >> 1));

            SetFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0x0000);
            SetFlag(FLAGS6502.N, (temp & 0x0080) == 0x0080);
            if (lookup[opcode].addrmode == IMP)
                a = (byte)(temp & 0x00FF);
            else
                Write(addr_abs, (byte)(temp & 0x00FF));

            return 0x00;
        }

        /// <summary>
        /// Return from Interrupt.
        /// Function:  pull SR, pull PC
        /// Flags Out: from stack
        /// </summary>
        byte RTI()
        {
            stkp++;
            status = Read((ushort)(0x0100 + stkp));
            SetFlag(FLAGS6502.B, false);
            SetFlag(FLAGS6502.U, false);
            // status &= (byte)~FLAGS6502.B;
            // status &= (byte)~FLAGS6502.U;

            stkp++;
            pc = (ushort)Read((ushort)(0x0100 + stkp));
            stkp++;
            pc |= (ushort)(Read((ushort)(0x0100 + stkp)) << 8);

            return 0x00;
        }

        /// <summary>
        /// Return from Subroutine
        /// Function:  pull PC, PC+1 -> PC
        /// Flags Out: -
        /// </summary>
        byte RTS()
        {
            stkp++;
            byte lo = Read((ushort)(0x0100 + stkp));
            stkp++;
            byte hi = Read((ushort)(0x0100 + stkp));

            pc = (ushort)((hi << 8) | lo);
            pc++;

            return 0x00;
        }

        /// <summary>
        /// Subtract Memory from Accumulator with Borrow.
        /// Function:  A - M - C -> A
        /// Flags Out: N, Z, C, V
        /// </summary>
        byte SBC()
        {
            fetch();

            ushort value = (ushort)((fetched) ^ 0x00FF);

            // perform addition in 16-bit domain
            ushort temp = (ushort)(a + value + (GetFlag(FLAGS6502.C) ? 1 : 0));

            SetFlag(FLAGS6502.C, temp > 255);            // Set carry flag
            SetFlag(FLAGS6502.Z, (temp & 0x00FF) == 0);  // Set zero flag
            SetFlag(FLAGS6502.N, (temp & 0x80) == 0x80); // Set the overflow flag
            SetFlag(FLAGS6502.V, ((~(a ^ fetched) & (a ^ temp)) & 0x0080) == 0x0080);

            /**
             * Truth table for working out whether to set the oVerflow flag
             * 
             * A    M    R  |  V   A^R A^M ~(A^M)
             * 0    0    0  |  0   0   0   1
             * 0    0    1  |  1   1   0   1       // We need to set V-Flag in this case
             * 0    1    0  |  0   0   1   0
             * 0    1    1  |  0   1   1   0
             * 1    0    0  |  0   1   1   0
             * 1    0    1  |  0   0   1   0
             * 1    1    0  |  1   1   0   1        // We need to set V-Flag in this case
             * 1    1    1  |  0   0   0   1
             * 
             * V = (A^R) & ~(A^M)
             * 
             */

            // finally store the result back in the accumulator
            a = (byte)(temp & 0x00FF);

            return 1;
        }

        /// <summary>
        /// Set Carry Flag
        /// Function:  1 -> C
        /// Flags Out: C
        /// </summary>
        byte SEC()
        {
            SetFlag(FLAGS6502.C, true);
            return 0x00;
        }

        /// <summary>
        /// Set Decimal Flag
        /// Function:  1 -> D
        /// Flags Out: D
        /// </summary>
        byte SED()
        {
            SetFlag(FLAGS6502.D, true);
            return 0x00;
        }

        /// <summary>
        /// Set Inerupt Flag
        /// Function:  1 -> I
        /// Flags Out: I
        /// </summary>
        byte SEI()
        {
            SetFlag(FLAGS6502.I, true);
            return 0x00;
        }

        /// <summary>
        /// Store Accumulator in Memory
        /// Function:  A -> M 
        /// Flags Out: -
        /// </summary>
        byte STA()
        {
            Write(addr_abs, a);
            return 0x00;
        }

        /// <summary>
        /// Store Index X in Memory
        /// Function:  X -> M 
        /// Flags Out: -
        /// </summary>
        byte STX()
        {
            Write(addr_abs, x);
            return 0x00;
        }

        /// <summary>
        /// Store Index Y in Memory
        /// Function:  Y -> M 
        /// Flags Out: -
        /// </summary>
        byte STY()
        {
            Write(addr_abs, y);
            return 0x00;
        }

        /// <summary>
        /// Transfer Accumulator to Index X
        /// Function:  A -> X
        /// Flags Out: N, Z
        /// </summary>
        byte TAX()
        {
            x = a;

            SetFlag(FLAGS6502.Z, x == 0x00);
            SetFlag(FLAGS6502.N, (x & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Transfer Accumulator to Index Y
        /// Function:  A -> Y
        /// Flags Out: N, Z
        /// </summary>
        byte TAY()
        {
            y = a;

            SetFlag(FLAGS6502.Z, y == 0x00);
            SetFlag(FLAGS6502.N, (y & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Transfer Stack Pointer to Index X
        /// Function:  SP -> X
        /// Flags Out: N, Z
        /// </summary>
        byte TSX()
        {
            x = stkp;

            SetFlag(FLAGS6502.Z, x == 0x00);
            SetFlag(FLAGS6502.N, (x & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Transfer Index X to Accumulator
        /// Function:  X -> A
        /// Flags Out: N, Z
        /// </summary>
        byte TXA()
        {
            a = x;

            SetFlag(FLAGS6502.Z, a == 0x00);
            SetFlag(FLAGS6502.N, (a & 0x80) == 0x80);

            return 0x00;
        }

        /// <summary>
        /// Transfer Index X to Stack Register
        /// Function:  X -> Stack Pointer
        /// Flags Out: -
        /// </summary>
        byte TXS()
        {
            stkp = x;
            return 0x00;
        }

        /// <summary>
        /// Transfer Index Y to Accumulator
        /// Function:  X -> Stack Pointer
        /// Flags Out: Z, N
        /// </summary>
        byte TYA()
        {
            a = y;

            SetFlag(FLAGS6502.Z, a == 0x00);
            SetFlag(FLAGS6502.N, (a & 0x80) == 0x80);

            return 0x00;
        }
        #endregion

        #region Constructor
        public CPU6502()
        {
            x = 0x00;
            a = 0x00;
            y = 0x00;
            stkp = 0xFD;

            CreateOpCodeLookup();
        }
        #endregion

        #region Methods
        public void ConnectBus(Bus n)
        {
            bus = n;
        }

        public void Write(ushort addr, byte data)
        {
            bus.Write(addr, data);
        }

        public byte Read(ushort addr)
        {
            return bus.Read(addr, false);
        }

        // The following signals can occur at any time and need to behave asynchronously.
        // They interupt the processor from doing what it is currently doing. The processor
        // will finish the current instruction being executed.

        /// <summary>
        /// Reset Signal.
        /// Reset interrupts are triggered when the system first starts and when the user 
        /// presses the reset button. When a reset occurs the system jumps to the address 
        /// located at $FFFC and $FFFD.
        /// </summary>
        public void Reset()
        {
            a = 0;
            x = 0;
            y = 0;
            stkp = 0xFD;
            status = (byte)(0x00 | FLAGS6502.U);

            // Set the PC to the address in the reset vector
            addr_abs = 0xFFFC;
            ushort loByte = Read((ushort)(addr_abs + 0)); // 0xFFFC
            ushort hiByte = Read((ushort)(addr_abs + 1)); // 0xFFFD

            pc = (ushort)((hiByte << 8) | loByte);

            // (re)Set emulator internal variables
            addr_rel = 0x0000;
            addr_abs = 0x0000;
            fetched = 0x00;

            cycles = 8;

        }

        // Interupt request signal
        // These can be ignored, depending on the status of the status register->I
        public void irq()
        {
            if (GetFlag(FLAGS6502.I))
            {
                // Service the interupt
                // Push the PC to the stack
                Write((ushort)(0x0100 + stkp), (byte)((pc >> 8) & 0x00FF));
                stkp--;
                Write((ushort)(0x0100 + stkp), (byte)(pc & 0x00FF));
                stkp--;

                SetFlag(FLAGS6502.B, false); // Break
                SetFlag(FLAGS6502.U, true); // Unused
                SetFlag(FLAGS6502.I, true); // Interupt

                Write((ushort)(0x0100 + stkp), status);
                stkp--;

                // Get the value of the new pc
                addr_abs = 0xFFFE;
                ushort loByte = Read((ushort)(addr_abs + 0));
                ushort hiByte = Read((ushort)(addr_abs + 1));

                pc = (ushort)((hiByte << 8) | loByte);

                // interupts take time
                cycles = 7;

            }
        }

        // Non-maskable interupt request signal
        // These can never be disabled.
        public void nmi()
        {
            // Service the interupt
            // Push the PC to the stack
            Write((ushort)(0x0100 + stkp), (byte)((pc >> 8) & 0x00FF));
            stkp--;
            Write((ushort)(0x0100 + stkp), (byte)(pc & 0x00FF));
            stkp--;

            SetFlag(FLAGS6502.B, false); // Break
            SetFlag(FLAGS6502.U, true); // Unused
            SetFlag(FLAGS6502.I, true); // Interupt

            Write((ushort)(0x0100 + stkp), status);
            stkp--;

            // Get the value of the new pc
            addr_abs = 0xFFFA;
            ushort loByte = Read((ushort)(addr_abs + 0)); // 0xFFFA
            ushort hiByte = Read((ushort)(addr_abs + 1)); // 0xFFFB

            pc = (ushort)((hiByte << 8) | loByte);

            // interupts take time
            cycles = 8;
        }

        /// <summary>
        /// The read location of data can come from two sources, either a memory address, or its
        /// immediately available as part of the instruction. This function decides depending
        /// on the address mode of the instruction byte.
        /// </summary>
        private byte fetch()
        {
            // Fetch data for all instructions with the exception of IMP addressing mode (because in this case,
            // there is no data to fetch).
            // This depends on the OpCode having already set the addr_abs variable.
            // n.b. the addr_rel is only used by the branching opcodes, and so is not used here.

            if (!(lookup[opcode].addrmode == IMP))
                fetched = Read(addr_abs);

            return fetched;
        }

        /// <summary>
        /// Creates an OpCode instance in the lookup table for each of the legal 6502 opcodes
        /// See docs/rockwell_r650x_651x.pdf page 10
        /// 
        /// This is a recreation of all elements of the 16x16 grid in a list, such that the opcodes are 
        /// identifable by their hexadecimal coordinates.
        /// </summary>
        private void CreateOpCodeLookup()
        {
            // Each Instruction includes:
            //   1 - mnemonic
            //   2 - opcode function
            //   3 - addressing mode
            //   4 - byte size
            //   5 - cycles
            lookup = new List<Instruction>();

            lookup.Add(new Instruction("BRK", BRK, IMP, 7)); lookup.Add(new Instruction("ORA", ORA, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ORA", ORA, ZP0, 3)); lookup.Add(new Instruction("ASL", ASL, ZP0, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("PHP", PHP, IMP, 3)); lookup.Add(new Instruction("ORA", ORA, IMM, 2)); lookup.Add(new Instruction("ASL", ASL, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ORA", ORA, ABS, 4)); lookup.Add(new Instruction("ASL", ASL, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BPL", BPL, REL, 2)); lookup.Add(new Instruction("ORA", ORA, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ORA", ORA, ZPX, 4)); lookup.Add(new Instruction("ASL", ASL, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CLC", CLC, IMP, 2)); lookup.Add(new Instruction("ORA", ORA, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ORA", ORA, ABX, 4)); lookup.Add(new Instruction("ASL", ASL, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("JSR", JSR, ABS, 6)); lookup.Add(new Instruction("AND", AND, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("BIT", BIT, ZP0, 3)); lookup.Add(new Instruction("AND", AND, ZP0, 3)); lookup.Add(new Instruction("ROL", ROL, ZP0, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("PLP", PLP, IMP, 4)); lookup.Add(new Instruction("AND", AND, IMM, 2)); lookup.Add(new Instruction("ROL", ROL, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("BIT", BIT, ABS, 4)); lookup.Add(new Instruction("AND", AND, ABS, 4)); lookup.Add(new Instruction("ROL", ROL, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BMI", BMI, REL, 2)); lookup.Add(new Instruction("AND", AND, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("AND", AND, ZPX, 4)); lookup.Add(new Instruction("ROL", ROL, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("SEC", SEC, IMP, 2)); lookup.Add(new Instruction("AND", AND, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("AND", AND, ABX, 4)); lookup.Add(new Instruction("ROL", ROL, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("RTI", RTI, IMP, 6)); lookup.Add(new Instruction("EOR", EOR, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("EOR", EOR, ZP0, 3)); lookup.Add(new Instruction("LSR", LSR, ZP0, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("PHA", PHA, IMP, 3)); lookup.Add(new Instruction("EOR", EOR, IMM, 2)); lookup.Add(new Instruction("LSR", LSR, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("JMP", JMP, ABS, 3)); lookup.Add(new Instruction("EOR", EOR, ABS, 4)); lookup.Add(new Instruction("LSR", LSR, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BVC", BVC, REL, 2)); lookup.Add(new Instruction("EOR", EOR, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("EOR", EOR, ZPX, 4)); lookup.Add(new Instruction("LSR", LSR, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CLI", CLI, IMP, 2)); lookup.Add(new Instruction("EOR", EOR, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("EOR", EOR, ABX, 4)); lookup.Add(new Instruction("LSR", LSR, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("RTS", RTS, IMP, 6)); lookup.Add(new Instruction("ADC", ADC, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ADC", ADC, ZP0, 3)); lookup.Add(new Instruction("ROR", ROR, ZP0, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("PLA", PLA, IMP, 4)); lookup.Add(new Instruction("ADC", ADC, IMM, 2)); lookup.Add(new Instruction("ROR", ROR, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("JMP", JMP, IND, 5)); lookup.Add(new Instruction("ADC", ADC, ABS, 4)); lookup.Add(new Instruction("ROR", ROR, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BVS", BVS, REL, 2)); lookup.Add(new Instruction("ADC", ADC, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ADC", ADC, ZPX, 4)); lookup.Add(new Instruction("ROR", ROR, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("SEI", SEI, IMM, 2)); lookup.Add(new Instruction("ADC", ADC, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("ADC", ADC, ABX, 4)); lookup.Add(new Instruction("ROR", ROR, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("???", NOP, IMP, 2)); lookup.Add(new Instruction("STA", STA, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("STY", STY, ZP0, 3)); lookup.Add(new Instruction("STA", STA, ZP0, 3)); lookup.Add(new Instruction("STX", STX, ZP0, 3)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("DEY", DEY, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("TXA", TXA, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("STY", STY, ABS, 4)); lookup.Add(new Instruction("STA", STA, ABS, 4)); lookup.Add(new Instruction("STX", STX, ABS, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BCC", BCC, REL, 2)); lookup.Add(new Instruction("STA", STA, IZY, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("STY", STY, ZPX, 4)); lookup.Add(new Instruction("STA", STA, ZPX, 4)); lookup.Add(new Instruction("STX", STX, ZPY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("TYA", TYA, IMP, 2)); lookup.Add(new Instruction("STA", STA, ABY, 5)); lookup.Add(new Instruction("TXS", TXS, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("STA", STA, ABX, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("LDY", LDY, IMM, 2)); lookup.Add(new Instruction("LDX", LDX, IZX, 6)); lookup.Add(new Instruction("LDX", LDX, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("LDY", LDY, ZP0, 3)); lookup.Add(new Instruction("LDA", LDA, ZP0, 3)); lookup.Add(new Instruction("LDX", LDX, ZP0, 3)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("TAY", TAY, IMP, 2)); lookup.Add(new Instruction("LDA", LDA, IMM, 2)); lookup.Add(new Instruction("TAX", TAX, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("LDY", LDY, ABS, 4)); lookup.Add(new Instruction("LDA", LDA, ABS, 4)); lookup.Add(new Instruction("LDX", LDX, ABS, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BCS", BCS, REL, 2)); lookup.Add(new Instruction("LDA", LDA, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("LDY", LDY, ZPX, 4)); lookup.Add(new Instruction("LDA", LDA, ZPX, 4)); lookup.Add(new Instruction("LDX", LDX, ZPY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CLV", CLV, IMP, 2)); lookup.Add(new Instruction("LDA", LDA, ABY, 4)); lookup.Add(new Instruction("TSX", TSX, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("LDY", LDY, ABX, 4)); lookup.Add(new Instruction("LDA", LDA, ABX, 4)); lookup.Add(new Instruction("LDX", LDX, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("CPY", CPY, IMM, 2)); lookup.Add(new Instruction("CMP", CMP, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CPY", CPY, ZP0, 3)); lookup.Add(new Instruction("CMP", CMP, ZP0, 3)); lookup.Add(new Instruction("DEC", DEC, ZP0, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("INY", INY, IMP, 2)); lookup.Add(new Instruction("CMP", CMP, IMM, 2)); lookup.Add(new Instruction("DEX", DEX, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CPY", CPY, ABS, 4)); lookup.Add(new Instruction("CMP", CMP, ABS, 4)); lookup.Add(new Instruction("DEC", DEC, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BNE", BNE, REL, 2)); lookup.Add(new Instruction("CMP", CMP, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CMP", CMP, ZPX, 4)); lookup.Add(new Instruction("DEC", DEC, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CLD", CLD, IMP, 2)); lookup.Add(new Instruction("CMP", CMP, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CMP", CMP, ABX, 4)); lookup.Add(new Instruction("DEC", DEC, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("CPX", CPX, IMM, 2)); lookup.Add(new Instruction("SBC", SBC, IZX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CPX", CPX, ZP0, 3)); lookup.Add(new Instruction("SBC", SBC, ZP0, 3)); lookup.Add(new Instruction("INC", INC, ZP0, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("INX", INX, IMP, 2)); lookup.Add(new Instruction("SBC", SBC, IMM, 2)); lookup.Add(new Instruction("NOP", NOP, IMP, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("CPX", CPX, ABS, 4)); lookup.Add(new Instruction("SBC", SBC, ABS, 4)); lookup.Add(new Instruction("INC", INC, ABS, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2));
            lookup.Add(new Instruction("BEQ", BEQ, REL, 2)); lookup.Add(new Instruction("SBC", SBC, IZY, 5)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("SBC", SBC, ZPX, 4)); lookup.Add(new Instruction("INC", INC, ZPX, 6)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("SED", SED, IMP, 2)); lookup.Add(new Instruction("SBC", SBC, ABY, 4)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("???", NOP, IMM, 2)); lookup.Add(new Instruction("SBC", SBC, ABX, 4)); lookup.Add(new Instruction("INC", INC, ABX, 7)); lookup.Add(new Instruction("???", NOP, IMM, 2));

        }
       
        /// <summary>
        /// Helper function for setting the bits of the processor status register.
        /// </summary>
        /// <param name="flag">The flag to set</param>
        /// <param name="v">The value of the bit [0,1]</param>
        private void SetFlag(FLAGS6502 flag, bool v)
        {
            if (v)
                // Set CPU status using flag mask and LOG OR 
                status |= (byte)flag;
            else
                // set CPU status using inverse of flag mask and LOG AND
                status &= (byte)~flag;
        }

        /// <summary>
        /// Helper function to get the state of a specific bit of the status register.
        /// </summary>
        /// <param name="flag">The bit flag to return</param>
        /// <returns>1 or 0</returns>
        public bool GetFlag(FLAGS6502 flag)
        {
            return (byte)(status & (byte)flag) > 0 ? true : false;

        }

        /// <summary>
        /// Completes 1 instruction
        /// </summary>
        public void stepOneInstruction()
        {
            do
            {
                clock();
            } while (!CycleComplete());
        }

        /// <summary>
        /// Proceeds by 1 clock cycle
        /// </summary>
        public void clock()
        {
            // NOTE: This implementation means that this emulation will not be clock-cycle accurate.
            // The actual NES hardware would execute the opcodes over time - here, we process an entire opcode
            // immiedately and wait the remaining time away.

            // If we have expended all cycles (from the last opcode), progress the emulation forward
            if (cycles == 0)
            {
                opcode = Read(pc); // use this byte to index the opcode LUT
                pc++;

                Instruction CurrentInstruction = lookup[opcode];

                System.Console.WriteLine($"[{pc - 1}] : {CurrentInstruction.name} ({String.Format("{0,2:X}", opcode)})");

                // Get the number of cycles required for this opcode
                cycles = CurrentInstruction.cycles;

                byte additionalCycle1 = CurrentInstruction.addrmode.Invoke();
                byte additionalCycle2 = CurrentInstruction.operate.Invoke();

                // Increase clock cycles only if BOTH the op and addr require it
                cycles += (additionalCycle1 & additionalCycle2);

            }

            // Everytime we call this function, 1 cycle has elapsed. 
            // remember, most instructions need 2+ cycles to complete
            cycles--;

        }

        public bool CycleComplete()
        {
            return cycles == 0;
        }

        public Instruction GetInstruction(byte opCode)
        {
            return lookup[opCode];
        }
        
        public void LoadDisassembly(ushort start, ushort end)
        {
            Disassembly = new SortedList();
            StringBuilder sb = new StringBuilder();

            ushort addr = start;
            byte data = 0x00;
            byte lo = 0x00;
            byte hi = 0x00;

            while(addr < end)
            {
                sb.Clear();

                // Prefix line with instruction address
                sb.Append(String.Format("{0,4:X4}: ", addr));

                ushort line_addr = addr;

                byte cur_opcode = bus.Read(addr, true); addr++;
                Instruction cur_instruction = GetInstruction(cur_opcode);
                sb.Append($"{cur_instruction.name} ");

                if(cur_instruction.addrmode == IMP)
                {
                    sb.Append("(IMP)");

                } else if (cur_instruction.addrmode == IMM)
                {
                    sb.Append(String.Format("#${0,2:X2} (IMM)", bus.Read(addr, true))); addr++;

                }
                else if (cur_instruction.addrmode == ABS)
                {
                    lo = bus.Read(addr, true); addr++;
                    hi = bus.Read(addr, true); addr++;
                    sb.Append(String.Format("${0,2:X2}", hi));
                    sb.Append(String.Format("{0,2:X2} (ABS)", lo));

                }
                else if (cur_instruction.addrmode == IND)
                {
                    lo = bus.Read(addr, true); addr++;
                    hi = bus.Read(addr, true); addr++;
                    sb.Append(String.Format("#${0,2:X4} (IND)", (hi << 8) | lo));

                }
                else if (cur_instruction.addrmode == REL)
                {
                    data = bus.Read(addr, true); addr++;
                    sb.Append(String.Format("${0,0:X2} ", data));
                    sb.Append(String.Format("[${0,4:X4}] (REL)", addr + data));

                }

                // TODO: Add remaining addr modes to disassembly (once we have a ROM loader)

                // Finish up this line
                Disassembly.Add(line_addr, sb.ToString());
            }

            DisassemblyLoaded = true;

        }

        public void LoadROM(String byteString)
        {
            DisassemblyLoaded = false;

            ushort offset = 0x8000;

            byte[] progArray = StringToByteArray(byteString);
            int progSize = progArray.Length;
            for (int i = 0; i < progSize; i++)
            {
                bus.Write(offset++, progArray[i]);

            }
        }

        public static byte[] StringToByteArray(String hex)
        {
            hex = hex.Replace(" ", "");
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];

            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

            return bytes;
        }

        #endregion
    }
}