using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NESSharp.Graphics;
using NESSharp.Input;
using NESSharp.Time;

namespace NESSharp
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

        #region Events
        public delegate void HandleInput(InputUtil inputUtil);
        public delegate void HandleUpdate(TimeUtil timeUtil);
        public delegate void HandleRender(DirectBitmap bitmap);

        public event HandleInput DoOnInput;
        public event HandleUpdate DoOnUpdate;
        public event HandleRender DoOnRender;
        #endregion

        #region Variables
        private readonly DirectBitmap bitmap;
        private readonly TimeUtil timeUtil;
        private readonly FPSUtil fpsUtil;
        private readonly InputUtil inputUtil;
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
            
            // Capture the WinForms input and store the state in an array buffer
            inputUtil = new InputUtil();
            this.KeyDown += inputUtil.OnKeyDown;
            this.KeyUp += inputUtil.OnKeyUp;

            // Assign the DirectBitmap to the pixturebox
            pictureBox1.Image = bitmap.Bitmap;

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
                    int sleepTime = (int)(timeUtil.TargetElapsedTimeMilli - timeUtil.AccumulatedElapsedTimeMilli);

                    System.Threading.Thread.Sleep(sleepTime);
                    continue;
                }

                // Handle input
                DoOnInput?.Invoke(inputUtil);

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

                    DoOnUpdate?.Invoke(timeUtil);

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

                DoOnRender?.Invoke(bitmap);

                // TODO: refactor this to some debug overlay
                using (var g = System.Drawing.Graphics.FromImage(bitmap.Bitmap))
                {
                    // Create a font and a brush
                    Font drawFont = new Font("Arial", 8);
                    SolidBrush drawBrush = new SolidBrush(Color.White);

                    g.DrawString(timeUtil.ElapsedGameTimeMilli.ToString() + "ms", drawFont, drawBrush, new PointF(5, 5));

                }

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
        #endregion
    }
}
