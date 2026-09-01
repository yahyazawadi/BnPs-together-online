using System;

namespace BnPRelay
{
    /// <summary>
    /// Encodes/decodes 6 directional+action keys into a single byte bitmask.
    /// Bit layout: [5]CANCEL [4]CONFIRM [3]RIGHT [2]DOWN [1]LEFT [0]UP
    /// </summary>
    public struct InputBitmask
    {
        public byte Value;

        public bool Up     { get => (Value & 0x01) != 0; set => Set(0, value); }
        public bool Left   { get => (Value & 0x02) != 0; set => Set(1, value); }
        public bool Down   { get => (Value & 0x04) != 0; set => Set(2, value); }
        public bool Right  { get => (Value & 0x08) != 0; set => Set(3, value); }
        public bool Confirm{ get => (Value & 0x10) != 0; set => Set(4, value); }
        public bool Cancel { get => (Value & 0x20) != 0; set => Set(5, value); }

        private void Set(int bit, bool on)
        {
            if (on) Value |= (byte)(1 << bit);
            else    Value &= (byte)~(1 << bit);
        }

        public static InputBitmask From(byte raw) => new InputBitmask { Value = raw };
        public override string ToString() =>
            $"U:{(Up?1:0)} L:{(Left?1:0)} D:{(Down?1:0)} R:{(Right?1:0)} Z:{(Confirm?1:0)} X:{(Cancel?1:0)}";
    }
}
