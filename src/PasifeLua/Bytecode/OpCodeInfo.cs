namespace PasifeLua.Bytecode
{
    public enum OpArgMode
    {
        N, //Not used
        U, //Used
        R, //Register
        K //Constant
    }
    public static class OpCodeInfo
    {
        public static int Length => OpModes.Length;
        static byte E(byte t, byte a, OpArgMode b, OpArgMode c, OpMode m)
        {
            return (byte)((t << 7) | (a << 6) | ((int)b << 4) | ((int)c << 2) | (int)m);
        }
        static readonly byte[] OpModes =
        {
            E(0, 1, OpArgMode.R, OpArgMode.N,  OpMode.iABC)		/* OP_MOVE */
            ,E(0, 1, OpArgMode.K, OpArgMode.N, OpMode.iABx)		/* OP_LOADK */
            ,E(0, 1, OpArgMode.N, OpArgMode.N, OpMode.iABx)		/* OP_LOADKX */
            ,E(0, 1, OpArgMode.U, OpArgMode.U, OpMode.iABC)		/* OP_LOADBOOL */
            ,E(0, 1, OpArgMode.U, OpArgMode.N, OpMode.iABC)		/* OP_LOADNIL */
            ,E(0, 1, OpArgMode.U, OpArgMode.N, OpMode.iABC)		/* OP_GETUPVAL */
            ,E(0, 1, OpArgMode.U, OpArgMode.K, OpMode.iABC)		/* OP_GETTABUP */
            ,E(0, 1, OpArgMode.R, OpArgMode.K, OpMode.iABC)		/* OP_GETTABLE */
            ,E(0, 0, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_SETTABUP */
            ,E(0, 0, OpArgMode.U, OpArgMode.N, OpMode.iABC)		/* OP_SETUPVAL */
            ,E(0, 0, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_SETTABLE */
            ,E(0, 1, OpArgMode.U, OpArgMode.U, OpMode.iABC)		/* OP_NEWTABLE */
            ,E(0, 1, OpArgMode.R, OpArgMode.K, OpMode.iABC)		/* OP_SELF */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_ADD */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_SUB */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_MUL */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_DIV */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_MOD */
            ,E(0, 1, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_POW */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iABC)		/* OP_UNM */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iABC)		/* OP_NOT */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iABC)		/* OP_LEN */
            ,E(0, 1, OpArgMode.R, OpArgMode.R, OpMode.iABC)		/* OP_CONCAT */
            ,E(0, 0, OpArgMode.R, OpArgMode.N, OpMode.iAsBx)		/* OP_JMP */
            ,E(1, 0, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_EQ */
            ,E(1, 0, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_LT */
            ,E(1, 0, OpArgMode.K, OpArgMode.K, OpMode.iABC)		/* OP_LE */
            ,E(1, 0, OpArgMode.N, OpArgMode.U, OpMode.iABC)		/* OP_TEST */
            ,E(1, 1, OpArgMode.R, OpArgMode.U, OpMode.iABC)		/* OP_TESTSET */
            ,E(0, 1, OpArgMode.U, OpArgMode.U, OpMode.iABC)		/* OP_CALL */
            ,E(0, 1, OpArgMode.U, OpArgMode.U, OpMode.iABC)		/* OP_TAILCALL */
            ,E(0, 0, OpArgMode.U, OpArgMode.N, OpMode.iABC)		/* OP_RETURN */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iAsBx)		/* OP_FORLOOP */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iAsBx)		/* OP_FORPREP */
            ,E(0, 0, OpArgMode.N, OpArgMode.U, OpMode.iABC)		/* OP_TFORCALL */
            ,E(0, 1, OpArgMode.R, OpArgMode.N, OpMode.iAsBx)		/* OP_TFORLOOP */
            ,E(0, 0, OpArgMode.U, OpArgMode.U, OpMode.iABC)		/* OP_SETLIST */
            ,E(0, 1, OpArgMode.U, OpArgMode.N, OpMode.iABx)		/* OP_CLOSURE */
            ,E(0, 1, OpArgMode.U, OpArgMode.N, OpMode.iABC)		/* OP_VARARG */
            ,E(0, 0, OpArgMode.U, OpArgMode.U, OpMode.iAx)		/* OP_EXTRAARG */
        };

        public static OpArgMode GetBMode(LuaOps c) => (OpArgMode) ((OpModes[(int)c] >> 4) & 3);
        public static OpArgMode GetCMode(LuaOps c) => (OpArgMode) ((OpModes[(int)c] >> 2) & 3);
        public static OpMode GetMode(LuaOps c) => (OpMode) ((OpModes[(int) c]) & 3);
        public static bool GetAMode(LuaOps c) => (OpModes[(int) c] & (1 << 6)) != 0;
        public static bool GetTMode(LuaOps c) => (OpModes[(int) c] & (1 << 7)) != 0;
    }
}