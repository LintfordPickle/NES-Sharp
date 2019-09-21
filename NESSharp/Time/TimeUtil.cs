using System;

namespace NESSharp.Time
{
    /// <summary>
    /// A utility class for handling time.
    /// </summary>
    public class TimeUtil
    {
        #region Variables
        private long LastFrame;
        #endregion

        #region Properties
        public double TotalGameTimeMilli { get; set; }
        public double ElapsedGameTimeMilli { get; set; }
        public double AccumulatedElapsedTimeMilli { get; set; }
        public double TargetElapsedTimeMilli { get; private set; }
        public double MaxElapsedTimeMilli { get; private set; }
        public Boolean IsGameRunningSlowly { get; set; }
        #endregion

        #region Constructor
        public TimeUtil()
        {
            MaxElapsedTimeMilli = 500;
            TargetElapsedTimeMilli = 16.666;

            GetDelta();
        }
        #endregion

        #region Methods
        public double GetDelta()
        {
            long time = Environment.TickCount; // ms resolution
            double lDelta = ((time - LastFrame) / 1.0);
            LastFrame = time;

            return lDelta;
        }

        public void ResetElapsedTime()
        {
            LastFrame = 0;
            TotalGameTimeMilli = 0.0f;
            ElapsedGameTimeMilli = 0.0f;
            TargetElapsedTimeMilli = 0.0f;
            MaxElapsedTimeMilli = 0.0f;
        }
        #endregion

    }
}
