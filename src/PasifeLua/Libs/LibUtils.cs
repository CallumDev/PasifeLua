using System;
using System.Diagnostics;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
     
    class LibUtils
    {
        [DebuggerHidden]
        public static void TypeError(int argNo, string func, LuaType expected, LuaType actual)
        {
            throw new Exception(
                $"bad argument #{argNo} to '{func}' ({expected.LuaName()} expected, got {actual.LuaName()})");
        }

        public static double GetNumber(LuaState state, int argNo, string func)
        {
            var x = state.Value(argNo);
            if (!x.TryGetNumber(out var n))
                TypeError(argNo, func, LuaType.Number, x.Type);
            return n;
        }
        
        public static string GetString(LuaState state, int argNo, string func)
        {
            var x = state.Value(argNo);
            if (!x.AsString(out var n))
                TypeError(argNo, func, LuaType.Number, x.Type);
            return n;
        }

        public static void ArgError(int argNo, string func, string reason)
        {
            throw new Exception($"bad argument #{argNo} to '{func}' ({reason})");
        }

        public static LuaTable CreateLib(LuaState state, string name,  (string, DelegateClrFunction)[] funcs = null)
        {
            var tab = new LuaTable();
            if (funcs != null) {
                foreach (var (n, func) in funcs)
                {
                    tab[n] = new LuaValue(func);
                }
            }
            state.AddLib(name, tab);
            return tab;
        }
    }
}