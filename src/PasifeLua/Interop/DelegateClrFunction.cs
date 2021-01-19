using System;

namespace PasifeLua.Interop
{
    public class DelegateClrFunction : ClrFunction
    {
        private readonly Func<LuaState, int> function;
        public DelegateClrFunction(Func<LuaState, int> impl)
        {
            function = impl;
        }
        public override int Run(LuaState state)
        {
            return function(state);
        }
    }
}