using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NESSharp;
using NESSharp.Graphics;
using NESSharp.Hardware;
using NESSharp.Input;
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

        private readonly DirectBitmap _Bitmap;
        private readonly TimeUtil _TimeUtil;
        private readonly FPSUtil _FpsUtil;
        private readonly InputUtil _InputUtil;
        private readonly Font _DrawFont;
        private readonly StringBuilder _StringBuilder;
        private readonly SolidBrush _DrawOnBrush;
        private readonly SolidBrush _DrawOffBrush;
        
        private float _InputTimer;
        private bool _InputStepCycle;
        private bool _InputReset;
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

            _Bitmap = new DirectBitmap(640, 480, PixelFormat.Format32bppArgb);
            _TimeUtil = new TimeUtil();
            _FpsUtil = new FPSUtil();
            _InputUtil = new InputUtil();

            this.KeyDown += _InputUtil.OnKeyDown;
            this.KeyUp += _InputUtil.OnKeyUp;

            // Assign the DirectBitmap to the pixturebox
            pictureBox1.Image = _Bitmap.Bitmap;

            _DrawFont = new Font("Courier New", 9);
            _StringBuilder = new StringBuilder();

            _DrawOnBrush = new SolidBrush(Color.Yellow);
            _DrawOffBrush = new SolidBrush(Color.Gray);

            _NESCore = new NESCore();

        }
        #endregion

        #region Methods
        public void LoadROM(String fileName)
        {
            Cartridge cart = new Cartridge(fileName);

            // temp
            _NESCore.InsertCartridge(cart);

            // Set the reset vector
            _NESCore.Reset();
        }
        private void Application_Idle(object sender, EventArgs e)
        {
            int updateFrameLag = 0;

            while (IsApplicationIdle())
            {
                // TODO: encapsulate this 
                _TimeUtil.AccumulatedElapsedTimeMilli += _TimeUtil.GetDelta();

                // Check if enough time has passed to do another update
                if (_TimeUtil.AccumulatedElapsedTimeMilli < _TimeUtil.TargetElapsedTimeMilli)
                {
                    // if not enough time has elapsed to process another update, then calculate the amount of time we 
                    // would need to wait and sleep the thread.
                    int sleepTime = (int)(_TimeUtil.TargetElapsedTimeMilli - _TimeUtil.AccumulatedElapsedTimeMilli);
                    
                    System.Threading.Thread.Sleep(sleepTime);
                    continue;
                }

                // Handle input
                OnInput(_InputUtil);

                // Do not allow any update to take longer than our maximum allowed.
                if (_TimeUtil.AccumulatedElapsedTimeMilli > _TimeUtil.MaxElapsedTimeMilli)
                    _TimeUtil.AccumulatedElapsedTimeMilli = _TimeUtil.MaxElapsedTimeMilli;

                _TimeUtil.ElapsedGameTimeMilli = _TimeUtil.TargetElapsedTimeMilli;
                int stepCount = 0;

                // Update as long as the accumulated time is higher than our target fixed step
                while (_TimeUtil.AccumulatedElapsedTimeMilli >= _TimeUtil.TargetElapsedTimeMilli)
                {
                    _TimeUtil.TotalGameTimeMilli += _TimeUtil.TargetElapsedTimeMilli;
                    _TimeUtil.AccumulatedElapsedTimeMilli -= _TimeUtil.TargetElapsedTimeMilli;
                    ++stepCount;

                    OnUpdate(_TimeUtil);

                }

                // Every update after the first accumulates lag
                updateFrameLag += Math.Max(0, stepCount - 1);

                if (_TimeUtil.IsGameRunningSlowly)
                {
                    if (updateFrameLag == 0)
                    {
                        _TimeUtil.IsGameRunningSlowly = false;

                    }

                }
                else if (updateFrameLag >= 5)
                {
                    // If we lag more than 5 frames, log we are running slowly (to which the app can react).
                    _TimeUtil.IsGameRunningSlowly = true;

                }

                // Draw needs to know the total elapsed time that occured for the fixed length updates.
                _TimeUtil.ElapsedGameTimeMilli = _TimeUtil.TargetElapsedTimeMilli * stepCount;
              
                _FpsUtil.Update(_TimeUtil);

                // Clear the screen buffer
                _Bitmap.FillColor(Color.Black.ToArgb());

                OnRender(_Bitmap);

                // Show the FPS and update steps in the window title
                Text = $"FPS {_FpsUtil.FramesPerSecond} u({stepCount})";

                // Invalidate the picture box and force a re-render
                pictureBox1.Invalidate();

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

            if (inputUtil.IsKeyPressed((byte)' '))
            {
                _InputStepCycle = true;
                _InputTimer = 0;

            }

            if (inputUtil.IsKeyPressed((byte)'R'))
            {
                _InputReset = true;
                _InputStepCycle = false;
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

            if (_InputStepCycle)
            {

                _InputStepCycle = false;

                // Emulate the cpu cycles until the next instruction completes
                do
                {
                    _NESCore.StepOne();

                } while (!_NESCore.IsCycleComplete());

            }
        }

        public void OnRender(DirectBitmap bitmap)
        {
            // Resolution of NES: 256 x 240

            // Clear the screen6
            bitmap.FillColor(Color.Blue.ToArgb());

            // Output debug information to screen
            using (var g = Graphics.FromImage(bitmap.Bitmap))
            {
                int Column0XPos = 5;
                int Column0YPos = 5;

                int Column1XPos = 425;
                int Column1YPos = 235;

                // Draw current CPU state
                DrawCPU(Column1XPos, Column0YPos, g);

                // Draw zero page
                DrawRAM(Column0XPos, Column0YPos, 0x0000, 16, 16, g);

                // Draw program memory
                DrawRAM(Column0XPos, Column1YPos, 0x8000, 16, 16, g);

                if (_NESCore.isROMLoaded && _NESCore.isDisassemblyLoaded)
                {
                    // Draw disassembler, if available
                    DrawDisassembler(Column1XPos, 167, 18, _NESCore.CPU().pc, g);

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

            int surroundLineCount = 10;

            int ourIndex = _NESCore.disassembly.IndexOfKey(addr);
            for (int i = ourIndex - surroundLineCount; i < ourIndex + surroundLineCount; i++)
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
        #endregion
    }
}
