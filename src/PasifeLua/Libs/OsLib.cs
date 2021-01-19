using System;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public class OsLib
    {
        private static DateTime T0 = DateTime.UtcNow;
        public static int clock(LuaState s)
        {
            var time = (DateTime.UtcNow - T0).TotalSeconds;
            s.Push(time < 0 ? 0.0 : time);
            return 1;
        }
        private static (string, DelegateClrFunction)[] funcs =
        {
            ("clock", new DelegateClrFunction(clock))
        };
        public static void Register(LuaState state)
        {
            LibUtils.CreateLib(state, "os", funcs);
        }
    }
}