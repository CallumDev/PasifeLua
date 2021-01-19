using System;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static class IoLib
    {
        public static int write(LuaState state)
        {
            int n = state.GetTop();
            for (int i = 1; i <= n; i++) {
                Console.Write(state.Value(i).ToString());
            }
            return 0;
        }

        public static int flush(LuaState state)
        {
            return 0;
        }

        private static readonly (string, DelegateClrFunction)[] funcs =
        {
            ("write", new DelegateClrFunction(write)),
            ("flush", new DelegateClrFunction(flush))
        };
        public static void Register(LuaState state)
        {
            LibUtils.CreateLib(state, "io", funcs);
        }
    }
}