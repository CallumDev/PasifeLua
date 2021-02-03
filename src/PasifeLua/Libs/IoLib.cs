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
                state.IO_currentout.Write(state.Value(i).ToString());
            }
            return 0;
        }
        
        public static int input(LuaState state)
        {
            if (state.GetTop() >= 1) {
                if (state.Value(1).AsString(out var s)) {
                    
                } 
                else if (state.Value(1).Value is LuaIOFile io) {
                    state.IO_currentin = io;
                }
                return 0;
            }
            else
            {
                state.Push(new LuaValue(LuaType.UserData, state.IO_currentin));
                return 1;
            }
        }
        
        public static int output(LuaState state)
        {
            if (state.GetTop() >= 1) {
                if (state.Value(1).AsString(out var s)) {
                    
                } 
                else if (state.Value(1).Value is LuaIOFile io) {
                    state.IO_currentout = io;
                }
                return 0;
            }
            else
            {
                state.Push(new LuaValue(LuaType.UserData, state.IO_currentout));
                return 1;
            }
        }

        public static int flush(LuaState state)
        {
            state.IO_currentout.Flush();
            return 0;
        }
        public static int type(LuaState state)
        {
            if (state.GetTop() < 1)
            {
                state.Push(new LuaValue());
                return 1;
            }
            if (state.Value(1).Value is LuaIOFile io)
            {
                if (io.Closed) state.Push(new LuaValue("closed file"));
                else state.Push(new LuaValue("file"));
            }
            else
            {
                state.Push(new LuaValue());
            }
            return 1;
        }
        
        public static int read(LuaState state)
        {
            string format = "*l";
            if (state.GetTop() > 0)
                format = LibUtils.GetString(state, 1, "read");
            state.Push(state.IO_currentin.Read(format));
            return 1;
        }

        private static readonly (string, DelegateClrFunction)[] funcs =
        {
            ("write", new DelegateClrFunction(write)),
            ("flush", new DelegateClrFunction(flush)),
            ("type", new DelegateClrFunction(type)),
            ("read", new DelegateClrFunction(read))
        };
        public static void Register(LuaState state)
        {
            var io = LibUtils.CreateLib(state, "io", funcs);
            io["stdin"] = new LuaValue(LuaType.UserData, state.IO_stdin);
            io["stdout"] = new LuaValue(LuaType.UserData, state.IO_stdout);
            io["stderr"] = new LuaValue(LuaType.UserData, state.IO_stderr);
        }
    }
}