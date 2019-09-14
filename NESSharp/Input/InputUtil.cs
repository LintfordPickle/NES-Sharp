using System.Windows.Forms;

namespace NESSharp.Input
{
    public class InputUtil
    {

        #region Properties
        public bool[] inputBuffer { get; private set; }
        #endregion

        #region Constructor
        public InputUtil()
        {
            inputBuffer = new bool[256];
        }
        #endregion

        #region Methods
        public bool IsKeyPressed(int pKeyCode)
        {
            return inputBuffer[pKeyCode];
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
