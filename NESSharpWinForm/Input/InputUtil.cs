using System.Windows.Forms;

namespace NESSharpWinForm.Input
{
    /// <summary>
    /// Utility function for handling input.
    /// </summary>
    public class InputUtil
    {
        #region Variables
        /// <summary>
        /// bool array of key states for ascii table
        /// </summary>
        public bool[] inputBuffer { get; private set; }
        #endregion

        #region Constructor
        public InputUtil()
        {
            inputBuffer = new bool[256];
        }
        #endregion

        #region Methods
        public bool IsKeyPressed(byte asciiCode)
        {
            if(asciiCode >= 0 && asciiCode <= 255)
                return inputBuffer[asciiCode];

            return false;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            inputBuffer[e.KeyValue] = true;
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            inputBuffer[e.KeyValue] = false;
        }
        #endregion
    }
}
