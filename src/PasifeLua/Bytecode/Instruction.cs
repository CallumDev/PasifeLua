using System.Runtime.CompilerServices;
using System.Text;

namespace PasifeLua.Bytecode
{ 
    public enum OpMode : byte
    {
        iABC,
        iABx,
        iAsBx,
        iAx
    }

    public struct Instruction
    {
        private uint packed;

        public Instruction(uint encoded)
        {
            packed = encoded;
        }

        public uint Encoded => packed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void setarg(uint v, int pos, int size)
        {
            var mask1 = ~((~(uint) 0) << size) << pos;
            var mask0 = ~mask1;

            packed = (packed & mask0) | (v << pos) & mask1;
        }
        
        public LuaOps Op {
            get => (LuaOps) (packed & 0x3F);
            set => setarg((uint) value, 0, 6);
        }

        //8 bit value
        public int A {
            get => (int) (packed >> 6) & 0xFF;
            set => setarg((uint) value, 6, 8);
        }

        //9 bit signed, -256 to 255
        public int B {
            get => (int) ((packed >> 23) & 0x1FF);
            set => setarg((uint) value, 23, 9);
        }

        //9 bit signed, -256 to 255
        public int C {
            get => (int) ((packed >> 14) & 0x1FF);
            set => setarg((uint) value, 14, 9);
        }

        //26 bit unsigned
        public int Ax {
            get => (int) (packed >> 6);
            set => setarg((uint) value, 14, 26);
        }

        public const int Bx_MAX = ((1 << 18)) - 1;
        public const int sBx_MAX = (Bx_MAX >> 1);

        //18 bit unsigned
        public uint Bx {
            get => packed >> 14;
            set => setarg(value, 14, 18);
        }

        //18 bit signed. 131071
        public int sBx { 
            get => (int) (packed >> 14) - sBx_MAX;
            set => setarg((uint) (value + sBx_MAX), 14, 18);
        }

        public override string ToString()
        {
            if ((int) Op <0 || (int) Op >= OpCodeInfo.Length)
                return $"0x{Op:X} - BADOP";
            OpMode mode = OpCodeInfo.GetMode(Op);
            StringBuilder builder = new StringBuilder();
            builder.Append(Op.ToString().PadRight(9));
            builder.Append(" ");
            switch (mode)
            {
                case OpMode.iABC:
                {
                    builder.Append(A);
                    int b = B;
                    if (b >= 256) b = -1 - (b - 256);
                    int c = C;
                    if (c >= 256) c = -1 - (c - 256);
                    if (OpCodeInfo.GetBMode(Op) != OpArgMode.N) builder.Append(" ").Append(b);
                    if (OpCodeInfo.GetCMode(Op) != OpArgMode.N) builder.Append(" ").Append(c);
                    break;
                }
                case OpMode.iAx:
                {
                    builder.Append(-1 - (int)Ax);
                    break;
                }
                case OpMode.iABx:
                {
                    builder.Append(A).Append(" ");
                    if (OpCodeInfo.GetBMode(Op) == OpArgMode.K) builder.Append(-1 - Bx);
                    else builder.Append(Bx);
                    break;
                }
                case OpMode.iAsBx:
                {
                    builder.Append(A).Append(" ").Append(sBx);
                    break;
                }
                default:
                    return $"{Op} - BADOP";
            }
            return builder.ToString();
        }
    }
}