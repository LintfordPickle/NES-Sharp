using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NESSharp.Graphics;
using NESSharp.Hardware;
using NESSharpWinForm.Input;
using NESSharp.Time;

namespace NESSharpWinForm
{
    public partial class MainForm : Form
    {
        #region Native
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        public static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);
        #endregion

        #region Variables
        private const float _InputWaitTime = 250; // ms to wait between keyboard input

        private readonly NESCore _NESCore;
        private Cartridge _cartridge;

        private readonly DirectBitmap _Bitmap;
        private readonly TimeUtil _TimeUtil;
        private readonly FPSUtil _FpsUtil;
        private readonly InputUtil _InputUtil;
        private readonly Font _DrawFont;
        private readonly StringBuilder _StringBuilder;
        private readonly SolidBrush _DrawOnBrush;
        private readonly SolidBrush _DrawOffBrush;

        // If true, the Application_Idle will force more updates at the expense of the render calls
        // to maintain a constant 'Updates Per Second'.
        private bool _EmulationMode;

        private float _InputTimer;
        private bool _InputStepFrame;
        private bool _InputStepCycle;
        private bool _InputReset;
        private int _selectedPalette;
        private bool _runToNextLDA;
        #endregion

        #region Properties
        public bool IsFixedTimeStep { get; protected set; }

        bool IsApplicationIdle()
        {
            NativeMessage result;
            return PeekMessage(out result, IntPtr.Zero, (uint)0, (uint)0, (uint)0) == 0;
        }
        #endregion

        #region Constructor
        public MainForm()
        {
            InitializeComponent();

            // Setup the real-time renderer to run when Application_Idle is fired.
            Application.Idle += Application_Idle;

            _Bitmap = new DirectBitmap(780, 480, PixelFormat.Format32bppArgb);
            _TimeUtil = new TimeUtil();
            _FpsUtil = new FPSUtil();
            _InputUtil = new InputUtil();

            this.KeyDown += _InputUtil.OnKeyDown;
            this.KeyUp += _InputUtil.OnKeyUp;

            // Assign the DirectBitmap to the pixturebox
            pictureBox1.Image = _Bitmap.bitmap;

            _DrawFont = new Font("Courier New", 9);
            _StringBuilder = new StringBuilder();

            _DrawOnBrush = new SolidBrush(Color.Yellow);
            _DrawOffBrush = new SolidBrush(Color.Gray);

            _NESCore = new NESCore();

        }
        #endregion

        #region Methods
        public bool LoadROM(String fileName)
        {
            _cartridge = new Cartridge(fileName);
            // TODO: Check valid ROM File loaded!

            _NESCore.InsertCartridge(_cartridge);

            // Load the ASM here, so it isn't integral to the NESBus
            // --->

            // Set the reset vector
            _NESCore.Reset();

            return true;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            while (IsApplicationIdle())
            {
                // Measure the amount of time since the last frame - we don't want to loop too quickly
                _TimeUtil.AccumulatedElapsedTimeMilli += _TimeUtil.GetDelta();

                // Check if enough time has passed to do another update
                if (_TimeUtil.AccumulatedElapsedTimeMilli < _TimeUtil.TargetElapsedTimeMilli)
                {
                    // ---> To sleep or simply exit?

                    // int sleepTime = (int)(_TimeUtil.TargetElapsedTimeMilli - _TimeUtil.AccumulatedElapsedTimeMilli);
                    // System.Threading.Thread.Sleep(sleepTime);

                    break;
                }

                // **************
                // Handle input

                OnInput(_InputUtil);
                
                _TimeUtil.ElapsedGameTimeMilli = _TimeUtil.TargetElapsedTimeMilli;
                
                _InputTimer += (float)_TimeUtil.ElapsedGameTimeMilli;

                // **************
                // Handle updates

                _FpsUtil.Update(_TimeUtil);

                OnUpdate(_TimeUtil);

                // **************
                // Handle Render

                OnRender(_Bitmap);

                // Invalidate the picture box and force a re-render
                pictureBox1.Invalidate();

                // Show the FPS and update steps in the window title
                Text = $"FPS {_FpsUtil.FramesPerSecond}  Emulation Mode: {_EmulationMode} Num CPU Cycles: {_NESCore.SystemClockCounter()}";

            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            pictureBox1.Height = Height;
            pictureBox1.Width = Width;
        }

        public void OnInput(InputUtil inputUtil)
        {
            if (_InputTimer < _InputWaitTime) return;

            // Toggle emulation mode on/off
            if (inputUtil.IsKeyPressed((byte)' '))
            {
                // If we are currently running to the next LDA instruction, then cancel that first
                // otherwise, toggle emulation mode on/off
                if (_runToNextLDA)
                {
                    _runToNextLDA = false;
                    _InputTimer = 0;

                } else
                {
                    _EmulationMode = !_EmulationMode;
                    _InputTimer = 0;

                    if (_EmulationMode)
                    {
                        _InputStepCycle = false;
                        _InputStepFrame = false;
                        _InputReset = false;
                    }
                }

                
            }                        

            // Step one CPU instruction
            if (!_EmulationMode && inputUtil.IsKeyPressed((byte)'C'))
            {
                _InputStepCycle = true;
                _InputTimer = 0;

            }

            // Run the ROM until the next occurance of the LDA instruction, then halt
            if (!_EmulationMode && inputUtil.IsKeyPressed((byte)'L'))
            {
                _runToNextLDA = true;
                _InputTimer = 0;

            }

            // Cycle through the available palettes.
            if (inputUtil.IsKeyPressed((byte)'P'))
            {
                _selectedPalette++;
                _selectedPalette &= 0x07;

            }

            // Step one PPU frame
            if (!_EmulationMode && inputUtil.IsKeyPressed((byte)'F'))
            {
                _InputStepFrame = true;
                _InputTimer = 0;

            }

            // Reset the NES
            if (inputUtil.IsKeyPressed((byte)'R'))
            {
                _InputReset = true;
                _InputStepCycle = false;
                _InputStepFrame = false;
                _runToNextLDA = false;
                _InputTimer = 0;

            }

        }

        public void OnUpdate(TimeUtil timeUtil)
        {
            _InputTimer += (float)timeUtil.ElapsedGameTimeMilli;

            if (_InputReset)
            {
                _InputReset = false;
                _NESCore.Reset();
            }

            // update the NES
            if (_EmulationMode)
            {
                do { _NESCore.clock(); } while (!_NESCore.PPU().frameComplete);
                _NESCore.PPU().frameComplete = false;

            }
            else
            {
                if (_runToNextLDA)
                {
                    // Check the end condition (LDA (IMM)) reached
                    if (_NESCore.CPU().opcode == 0xA1 ||
                        _NESCore.CPU().opcode == 0xA5 ||
                        _NESCore.CPU().opcode == 0xA9 ||
                        _NESCore.CPU().opcode == 0xAD ||
                        _NESCore.CPU().opcode == 0xB1 ||
                        _NESCore.CPU().opcode == 0xB5 ||
                        _NESCore.CPU().opcode == 0xB6 ||
                        _NESCore.CPU().opcode == 0xBD )
                    {
                        _runToNextLDA = false;
                    }

                    // advance one complete frame
                    _NESCore.StepCPUInstruction();
                    _InputStepCycle = false;

                }

                if (_InputStepFrame)
                {
                    _NESCore.StepPPUFrame();
                    _NESCore.PPU().frameComplete = false;
                    _InputStepFrame = false;
                }

                if (_InputStepCycle)
                {
                    _NESCore.StepCPUInstruction();
                    _InputStepCycle = false;

                }
            }
        }

        public void OnRender(DirectBitmap bitmap)
        {
            // Clear the screen
            bitmap.FillColor(Color.DarkBlue.ToArgb());

            // Output debug information to screen
            using (var g = Graphics.FromImage(bitmap.bitmap))
            {
                // Draw current CPU state
                DrawCPU(516, 5, g);

                // Draw zero page
                // DrawRAM(Column0XPos, Column0YPos, 0x0000, 16, 16, g);

                // Draw program memory
                // DrawRAM(Column0XPos, Column1YPos, 0x8000, 16, 16, g);

                DrawSprite(_NESCore.PPU().sprScreen, 0, 0, 0, 0, 256, 240, 2);

                DrawSprite(_NESCore.PPU().GetPatternTableSprite(0, (byte)_selectedPalette), 516, 348, 0, 0, 128, 128, 1);
                DrawSprite(_NESCore.PPU().GetPatternTableSprite(0, (byte)_selectedPalette), 648, 348, 0, 0, 128, 128, 1);

                if (_NESCore.isROMLoaded && _NESCore.isDisassemblyLoaded)
                {
                    // Draw disassembler, if available
                    DrawDisassembler(516, 95, 16, _NESCore.CPU().pc, g);

                }
            }
        }

        /// <summary>
        /// Renders the internal state of the 6502 CPU
        /// </summary>
        private void DrawCPU(int x, int y, Graphics g)
        {
            // Create a font and a brush
            bool IsBFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.B); // break
            bool IsCFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.C); // carry flag
            bool IsDFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.D); // decimal (mode)
            bool IsIFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.I); // interupt
            bool IsNFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.N); // negative
            bool IsUFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.U); // unused
            bool IsVFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.V); // overflow
            bool IsZFlagSet = _NESCore.CPU().GetFlag(CPU6502.FLAGS6502.Z); // zero

            _StringBuilder.Clear();
            // Render 'off' flags
            _StringBuilder.Append("       ");
            _StringBuilder.Append(IsBFlagSet ? "  " : "B ");
            _StringBuilder.Append(IsCFlagSet ? "  " : "C ");
            _StringBuilder.Append(IsDFlagSet ? "  " : "D ");
            _StringBuilder.Append(IsIFlagSet ? "  " : "I ");
            _StringBuilder.Append(IsNFlagSet ? "  " : "N ");
            _StringBuilder.Append(IsUFlagSet ? "  " : "U ");
            _StringBuilder.Append(IsVFlagSet ? "  " : "V ");
            _StringBuilder.Append(IsZFlagSet ? "  " : "Z ");

            g.DrawString(_StringBuilder.ToString(), _DrawFont, _DrawOffBrush, new PointF(x, y));

            // 
            _StringBuilder.Clear();
            _StringBuilder.Append("Flags: ");
            _StringBuilder.Append(IsBFlagSet ? "B " : "  ");
            _StringBuilder.Append(IsCFlagSet ? "C " : "  ");
            _StringBuilder.Append(IsDFlagSet ? "D " : "  ");
            _StringBuilder.Append(IsIFlagSet ? "I " : "  ");
            _StringBuilder.Append(IsNFlagSet ? "N " : "  ");
            _StringBuilder.Append(IsUFlagSet ? "U " : "  ");
            _StringBuilder.Append(IsVFlagSet ? "V " : "  ");
            _StringBuilder.Append(IsZFlagSet ? "Z " : "  ");

            g.DrawString(_StringBuilder.ToString(), _DrawFont, _DrawOnBrush, new PointF(x, y));

            _StringBuilder.Clear();
            _StringBuilder.Append(" \n");
            _StringBuilder.Append("PC: "); _StringBuilder.Append(String.Format("${0,2:X2} \n", _NESCore.CPU().pc));
            _StringBuilder.Append("A:  "); _StringBuilder.Append(String.Format("${0,2:X2} [{0,0:D}] \n", _NESCore.CPU().a));
            _StringBuilder.Append("X:  "); _StringBuilder.Append(String.Format("${0,2:X2} [{0,0:D}] \n", _NESCore.CPU().x));
            _StringBuilder.Append("Y:  "); _StringBuilder.Append(String.Format("${0,2:X2} [{0,0:D}] \n", _NESCore.CPU().y));
            _StringBuilder.Append("stkp:  "); _StringBuilder.Append(String.Format("${0,2:X4} \n", _NESCore.CPU().stkp));

            g.DrawString(_StringBuilder.ToString(), _DrawFont, _DrawOnBrush, new PointF(x, y));
        }

        /// <summary>
        /// Renders the contents of the ROM from the given offset to the screen.
        /// </summary>
        private void DrawRAM(int x, int y, ushort addr, int rows, int cols, Graphics g)
        {
            int PosX = x;
            int PosY = y;

            _StringBuilder.Clear();

            for (int row = 0; row < rows; row++)
            {
                _StringBuilder.Append(String.Format("${0,4:X4}: ", addr));

                for (int col = 0; col < cols; col++)
                {
                    _StringBuilder.Append(String.Format("{0,2:X2} ", _NESCore.CPURead(addr, true)));
                    addr++;
                }
                _StringBuilder.Append("\n");

            }

            g.DrawString(_StringBuilder.ToString(), _DrawFont, _DrawOnBrush, new PointF(x, y));

        }
        
        /// <summary>
        /// Renders the current disassembler relative to the PC.
        /// </summary>
        private void DrawDisassembler(int x, int y, int numLines, ushort addr, Graphics g)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Disassembly:");
            IList disassemblerKeyList = _NESCore.disassembly.GetValueList();

            // numlines above and below current instruction
            int halfLineCount = numLines / 2;

            int ourIndex = _NESCore.disassembly.IndexOfKey(addr);
            for (int i = ourIndex - halfLineCount; i < ourIndex + halfLineCount; i++)
            {
                if (i < 0)
                {
                    sb.AppendLine(" ");
                    continue;
                }

                if (i == ourIndex)
                    sb.AppendLine("-> " + disassemblerKeyList[(ushort)i].ToString());
                else
                    sb.AppendLine("   " + disassemblerKeyList[(ushort)i].ToString());
            }

            g.DrawString(sb.ToString(), _DrawFont, _DrawOnBrush, new PointF(x, y));
        }
        
        public void DrawSprite(Sprite spr, int dx, int dy, int sx, int sy, int sw, int sh, int scale)
        {
            for (int x = 0; x < sw * scale; x++)
            {
                for (int y = 0; y < sh * scale; y++)
                {
                    int srcX = x + sx;
                    int srcY = y + sy;
                    if (srcX < 0 || srcX >= spr.width * scale)
                        continue;
                    if (srcY < 0 || srcY >= spr.height * scale)
                        continue;

                    int dstX = dx + x;
                    int dstY = dy + y;

                    if (dstX < 0 || dstX > _Bitmap.width - 1)
                        continue;
                    if (dstY < 0 || dstY > _Bitmap.height - 1)
                        continue;

                    int srcP = spr.bitmap[srcY / scale * spr.width + srcX / scale];

                    _Bitmap.SetPixel(dstX, dstY, Color.FromArgb(srcP));
                }
            }
        }
        #endregion
    }
}
