using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PasifeLua
{
    public partial class LuaState
    {
        [DebuggerHidden]
        static void api_check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"api check failed {message}");
            }
        }

        void api_checknelems(int n)
        {
            api_check(n < (top - ci.Func), "not enough elements in the stack");
        }
        
        void api_incr_top()
        {
            top++;
            api_check(top <= ci.Top, "stack overflow");
        }
    }
}