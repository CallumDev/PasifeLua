using PasifeLua.Bytecode;
//statics
using static PasifeLua.luac.expkind;
using static PasifeLua.luac.RESERVED;

namespace PasifeLua
{
    //Constants and macros from lua 5.2 header files
    //Shared between ported luac, and runtime
    static class Constants
    {
        public static bool vkisvar(int k) => ((int) VLOCAL <= k && k <= (int) VINDEXED);
        public static bool vkisinreg(int k) => (k == (int) VNONRELOC || k == (int) VLOCAL);
        /* maximum number of local variables per function (must be smaller than 250, due to the bytecode format) */
        public const int MAXVARS = 200;
        public static bool hasmultret(luac.expkind k) => (k == VCALL || k ==  VVARARG);
        
        //from llimits.h
        public const int MAXUPVAL = 255;
        
        public const int NO_JUMP = -1;
        public const int NO_REG = byte.MaxValue;

        public const int MAXSTACK = 250;

        public const int MAXARG_A = 255;
        public const int MAXARG_B = 511;
        public const int MAXARG_C = 511;
        public const int MAXARG_Ax = (1 << 26) - 1;
        public const int MAXARG_Bx = Instruction.Bx_MAX;
        public const int MAXARG_sBx = Instruction.sBx_MAX;

        public const int MAXINDEXRK = (BITRK - 1);
        private const int BITRK = (1 << (8));
        
        public static bool ISK(int i) => (((i) & BITRK) != 0);
        public static int INDEXK(int r) => ((int)(r) & ~BITRK);
        public static int RKASK(int x) => ((x) | BITRK);
        
        public const int FIRST_RESERVED = 257;
        public const int NUM_RESERVED = (int)TK_WHILE - FIRST_RESERVED + 1;
        public const string LUA_ENV = "_ENV";
        public const int EOZ = -1; //end of stream (TextReader returns -1)
        
        public const int LUA_MULTRET = -1;
        public const int LFIELDS_PER_FLUSH = 50;

    }
}