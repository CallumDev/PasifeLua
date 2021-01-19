using System;
using PasifeLua.Interop;

namespace PasifeLua
{
    static class TM
    {
        private static string[] tmNames =
        {
            "__index", "__newindex",
            "__gc", "__mode", "__len", "__eq",
            "__add", "__sub", "__mul", "__div", "__mod",
            "__pow", "__unm", "__lt", "__le",
            "__concat", "__call", "__tostring"
        };

        public static LuaValue GetTMByObj(LuaState state, LuaValue val, TMS ev)
        {
            LuaTable mt = null;
            if (val.Type == LuaType.Table) mt = val.Table().MetaTable;
            TypeDescriptor td;
            if (val.Type == LuaType.UserData && (td = UserData.GetDescriptor(val.obj.GetType())) != null)
            {
                if (td.TMs[(int) ev] != null)
                    return new LuaValue(td.TMs[(int) ev]);
                else
                    return new LuaValue(LuaType.Nil);
            }
            if (mt != null)
                return mt[tmNames[(int) ev]];
            else
                return new LuaValue(LuaType.Nil);
        }
        public static LuaValue GetTMByTable(LuaState state, LuaTable tbl, TMS ev)
        {
            var mt = tbl.MetaTable;
            if (mt != null)
                return mt[tmNames[(int) ev]];
            else
                return new LuaValue(LuaType.Nil);
        }

    }

    enum TMS
    {
        INDEX,
        NEWINDEX,
        GC,
        MODE,
        LEN,
        EQ,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        POW,
        UNM,
        LT,
        LE,
        CONCAT,
        CALL,
        TOSTRING,
        N
    }
}