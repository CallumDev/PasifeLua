using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public class DebugLib
    {
        public static int getinfo(LuaState state)
        {
            return 0;
        }
        
        private static (string, DelegateClrFunction)[] funcs =
        {
            ("getinfo", new DelegateClrFunction(getinfo))
        };
        
        public static void Register(LuaState state)
        {
            var package = LibUtils.CreateLib(state, "debug", funcs);
        }
    }
}