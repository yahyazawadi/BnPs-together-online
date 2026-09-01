using System.Windows.Input;

namespace BnPRelay
{
    /// <summary>
    /// Maps any supported key combination (WASD or Arrow Keys, Z/F/Enter, X/G/Shift)
    /// to the canonical 6-bit InputBitmask for transmission.
    /// </summary>
    public static class InputNormalizer
    {
        public static InputBitmask FromKey(Key key, InputBitmask current, bool pressed)
        {
            var mask = current;
            switch (key)
            {
                // UP
                case Key.W: case Key.Up:
                    mask.Up = pressed; break;
                // LEFT
                case Key.A: case Key.Left:
                    mask.Left = pressed; break;
                // DOWN
                case Key.S: case Key.Down:
                    mask.Down = pressed; break;
                // RIGHT
                case Key.D: case Key.Right:
                    mask.Right = pressed; break;
                // CONFIRM — Z, F, or Enter
                case Key.Z: case Key.F: case Key.Return:
                    mask.Confirm = pressed; break;
                // CANCEL — X, G, or Shift
                case Key.X: case Key.G: case Key.LeftShift: case Key.RightShift:
                    mask.Cancel = pressed; break;
            }
            return mask;
        }

        /// <summary>
        /// Returns true if this key is one of the 6 captured relay keys.
        /// Used to suppress relayed keys from also affecting the local game as P1.
        /// </summary>
        public static bool IsRelayKey(Key key) => key switch
        {
            Key.W or Key.A or Key.S or Key.D => true,
            Key.F or Key.G => true,
            _ => false
        };
    }
}
