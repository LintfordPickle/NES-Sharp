using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
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
        private readonly DirectBitmap bitmap;
        private readonly TimeUtil timeUtil;
        private readonly FPSUtil fpsUtil;
        private readonly InputUtil inputUtil;
        private readonly Font drawFont;
        private readonly StringBuilder sb;
        private readonly SolidBrush drawOnBrush;
        private readonly SolidBrush drawOffBrush;
        
        private bool inputStepCycle;
        private bool inputReset;

        private NESCore NESCore;

        private float inputTimer;
        private float inputWaitTime = 250; // ms
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

            bitmap = new DirectBitmap(640, 480, PixelFormat.Format32bppArgb);
            timeUtil = new TimeUtil();
            fpsUtil = new FPSUtil();
            inputUtil = new InputUtil();

            this.KeyDown += inputUtil.OnKeyDown;
            this.KeyUp += inputUtil.OnKeyUp;

            // Assign the DirectBitmap to the pixturebox
            pictureBox1.Image = bitmap.Bitmap;

            drawFont = new Font("Courier New", 9);
            sb = new StringBuilder();

            drawOnBrush = new SolidBrush(Color.Yellow);
            drawOffBrush = new SolidBrush(Color.Gray);

            NESCore = new NESCore();

            // Load a basic program and generate disassembly
            // 3 x 10 and store result in $0002
            String program = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 38 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";
            NESCore.LoadROMFromString(program, true);

            // Set the reset vector
            NESCore.Reset();

        }
        #endregion

        #region Methods
        private void Application_Idle(object sender, EventArgs e)
        {
            int updateFrameLag = 0;

            while (IsApplicationIdle())
            {
                // TODO: encapsulate this 
                timeUtil.AccumulatedElapsedTimeMilli += timeUtil.GetDelta();

                // Check if enough time has passed to do another update
                if (timeUtil.AccumulatedElapsedTimeMilli < timeUtil.TargetElapsedTimeMilli)
                {
                    // if not enough time has elapsed to process another update, then calculate the amount of time we 
                    // would need to wait and sleep the thread.
                    int sleepTime = (int)(timeUtil.TargetElapsedTimeMilli - timeUtil.AccumulatedElapsedTimeMilli);
                    
                    System.Threading.Thread.Sleep(sleepTime);
                    continue;
                }

                // Handle input
                OnInput(inputUtil);

                // Do not allow any update to take longer than our maximum allowed.
                if (timeUtil.AccumulatedElapsedTimeMilli > timeUtil.MaxElapsedTimeMilli)
                    timeUtil.AccumulatedElapsedTimeMilli = timeUtil.MaxElapsedTimeMilli;

                timeUtil.ElapsedGameTimeMilli = timeUtil.TargetElapsedTimeMilli;
                int stepCount = 0;

                // Update as long as the accumulated time is higher than our target fixed step
                while (timeUtil.AccumulatedElapsedTimeMilli >= timeUtil.TargetElapsedTimeMilli)
                {
                    timeUtil.TotalGameTimeMilli += timeUtil.TargetElapsedTimeMilli;
                    timeUtil.AccumulatedElapsedTimeMilli -= timeUtil.TargetElapsedTimeMilli;
                    ++stepCount;

                    OnUpdate(timeUtil);

                }

                // Every update after the first accumulates lag
                updateFrameLag += Math.Max(0, stepCount - 1);

                if (timeUtil.IsGameRunningSlowly)
                {
                    if (updateFrameLag == 0)
                    {
                        timeUtil.IsGameRunningSlowly = false;

                    }

                }
                else if (updateFrameLag >= 5)
                {
                    // If we lag more than 5 frames, log we are running slowly (to which the app can react).
                    timeUtil.IsGameRunningSlowly = true;

                }

                // Draw needs to know the total elapsed time that occured for the fixed length updates.
                timeUtil.ElapsedGameTimeMilli = timeUtil.TargetElapsedTimeMilli * stepCount;
              
                fpsUtil.Update(timeUtil);

                // Clear the screen buffer
                bitmap.FillColor(Color.Black.ToArgb());

                OnRender(bitmap);

                // Show the FPS and update steps in the window title
                Text = $"FPS {fpsUtil.FramesPerSecond} u({stepCount})";

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
            if (inputTimer < inputWaitTime) return;

            if (inputUtil.IsKeyPressed((byte)' '))
            {
                inputStepCycle = true;
                inputTimer = 0;

            }

            if (inputUtil.IsKeyPressed((byte)'R'))
            {
                inputReset = true;
                inputStepCycle = false;
                inputTimer = 0;

            }

        }

        public void OnUpdate(TimeUtil timeUtil)
        {
            inputTimer += (float)timeUtil.ElapsedGameTimeMilli;

            if (inputReset)
            {
                inputReset = false;
                NESCore.Reset();
            }

            if (inputStepCycle)
            {

                inputStepCycle = false;

                // Emulate the cpu cycles until the next instruction completes
                do
                {
                    NESCore.NESCPU.clock();

                } while (!NESCore.NESCPU.CycleComplete());

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

                if (NESCore.NESCPU.DisassemblyLoaded)
                {
                    // Draw disassembler, if available
                    DrawDisassembler(Column1XPos, 167, 18, NESCore.NESCPU.pc, g);

                }
            }
        }

        /// <summary>
        /// Renders the internal state of the 6502 CPU
        /// </summary>
        private void DrawCPU(int x, int y, Graphics g)
        {
            // Create a font and a brush
            bool IsBFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.B); // break
            bool IsCFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.C); // carry flag
            bool IsDFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.D); // decimal (mode)
            bool IsIFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.I); // interupt
            bool IsNFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.N); // negative
            bool IsUFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.U); // unused
            bool IsVFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.V); // overflow
            bool IsZFlagSet = NESCore.NESCPU.GetFlag(CPU6502.FLAGS6502.Z); // zero

            sb.Clear();
            // Render 'off' flags
            sb.Append("       ");
            sb.Append(IsBFlagSet ? "  " : "B ");
            sb.Append(IsCFlagSet ? "  " : "C ");
            sb.Append(IsDFlagSet ? "  " : "D ");
            sb.Append(IsIFlagSet ? "  " : "I ");
            sb.Append(IsNFlagSet ? "  " : "N ");
            sb.Append(IsUFlagSet ? "  " : "U ");
            sb.Append(IsVFlagSet ? "  " : "V ");
            sb.Append(IsZFlagSet ? "  " : "Z ");

            g.DrawString(sb.ToString(), drawFont, drawOffBrush, new PointF(x, y));

            // 
            sb.Clear();
            sb.Append("Flags: ");
            sb.Append(IsBFlagSet ? "B " : "  ");
            sb.Append(IsCFlagSet ? "C " : "  ");
            sb.Append(IsDFlagSet ? "D " : "  ");
            sb.Append(IsIFlagSet ? "I " : "  ");
            sb.Append(IsNFlagSet ? "N " : "  ");
            sb.Append(IsUFlagSet ? "U " : "  ");
            sb.Append(IsVFlagSet ? "V " : "  ");
            sb.Append(IsZFlagSet ? "Z " : "  ");

            g.DrawString(sb.ToString(), drawFont, drawOnBrush, new PointF(x, y));

            sb.Clear();
            sb.Append(" \n");
            sb.Append("PC: "); sb.Append(String.Format("${0,2:X2} \n", NESCore.NESCPU.pc));
            sb.Append("A:  "); sb.Append(String.Format("${0,2:X2} [{0,0:D}] \n", NESCore.NESCPU.a));
            sb.Append("X:  "); sb.Append(String.Format("${0,2:X2} [{0,0:D}] \n", NESCore.NESCPU.x));
            sb.Append("Y:  "); sb.Append(String.Format("${0,2:X2} [{0,0:D}] \n", NESCore.NESCPU.y));
            sb.Append("stkp:  "); sb.Append(String.Format("${0,2:X4} \n", NESCore.NESCPU.stkp));

            g.DrawString(sb.ToString(), drawFont, drawOnBrush, new PointF(x, y));
        }

        /// <summary>
        /// Renders the contents of the ROM from the given offset to the screen.
        /// </summary>
        private void DrawRAM(int x, int y, ushort addr, int rows, int cols, Graphics g)
        {
            int PosX = x;
            int PosY = y;

            sb.Clear();

            for (int row = 0; row < rows; row++)
            {
                sb.Append(String.Format("${0,4:X4}: ", addr));

                for (int col = 0; col < cols; col++)
                {
                    sb.Append(String.Format("{0,2:X2} ", NESCore.NESBus.Read(addr, true)));
                    addr++;
                }
                sb.Append("\n");

            }

            g.DrawString(sb.ToString(), drawFont, drawOnBrush, new PointF(x, y));

        }

        /// <summary>
        /// Renders the current disassembler relative to the PC.
        /// </summary>
        private void DrawDisassembler(int x, int y, int numLines, ushort addr, Graphics g)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Disassembly:");
            IList disassemblerKeyList = NESCore.NESCPU.Disassembly.GetValueList();

            int surroundLineCount = 10;

            int ourIndex = NESCore.NESCPU.Disassembly.IndexOfKey(addr);
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

            g.DrawString(sb.ToString(), drawFont, drawOnBrush, new PointF(x, y));
        }



        #endregion
    }
}
