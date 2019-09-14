using NESSharp.Time;

namespace NESSharp.Graphics
{
    /// <summary>
    /// Updates each frame and measures the FPS
    /// </summary>
    class FPSUtil
    {
        #region Variables
        private int deltaFrameCount;
        private double timer;
        #endregion

        #region Properties
        public int FramesPerSecond { get; private set; }
        #endregion

        #region Methods
        public void Update(TimeUtil timeUtil)
        {
            deltaFrameCount++;

            timer += timeUtil.ElapsedGameTimeMilli;
            if (timer > 1000)
            {
                FramesPerSecond = deltaFrameCount;
                deltaFrameCount = 0;
                timer -= 1000;

            }
        }
        #endregion
    }
}
