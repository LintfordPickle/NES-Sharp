namespace NESSharp.Input
{

    public class InputUtil
    {
        #region Constants
        public const int D_UP = 0x00000000;
        public const int D_RIGHT = 0x00000000;
        public const int D_LEFT = 0x00000000;
        public const int D_DOWN = 0x00000000;

        public const int BUTTON_A = 0x00000000;
        public const int BUTTON_B = 0x00000000;
        public const int BUTTON_SELECT = 0x00000000;
        public const int BUTTON_SSTART = 0x00000000;

        public const int HARDWARE_POWER = 0x00000000;
        public const int HARDWARE_RESET = 0x00000000;
        #endregion

        #region Variables
        public bool[] inputBuffer { get; private set; }
        #endregion

        #region Constructor
        public InputUtil()
        {
            inputBuffer = new bool[10];
        }
        #endregion

        #region Methods
        public bool GetInputState(int inputByte)
        {
            if(inputByte >= 0 && inputByte <= 10)
                return inputBuffer[inputByte];

            return false;
        }

        public void SetInputState(int inputByte, bool isPressed)
        {
            if (inputByte >= 0 && inputByte <= 10)
                inputBuffer[inputByte] = isPressed;
        }
        #endregion

    }
}
