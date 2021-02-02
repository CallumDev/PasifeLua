using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PasifeLua.Bytecode;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static partial class BaseLib
    {
        public static int assert(LuaState state)
        {
            int n = state.GetTop();
            if (!state.Value(1).Boolean())
            {
                string message = (n > 1) ? state.Value(2).ToString() : "assertion failed!";
                throw new Exception(message);
            }
            return 0;
        }

        public static int collectgarbage(LuaState state)
        {
            return 0;
        }

        public static int error(LuaState state)
        {
            throw new Exception(state.Value(1).ToString());
        }
        
        public static int print(LuaState state)
        {
            int n = state.GetTop();
            for (int i = 1; i <= n; i++) {
                if(i > 1) state.StandardOut.Write("\t");
                state.StandardOut.Write(state.Value(i).ToString(state));
            }
            
            state.StandardOut.WriteLine();
            return 0;
        }

        public static int tostring(LuaState state)
        {
            state.Push(state.Value(1).ToString(state));
            return 1;
        }

        public static int dofile(LuaState state)
        {
            var path = state.Value(1).ToString();
            LuaChunk chunk;
            using (var stream = File.OpenRead(path)) {
                chunk = new LuaChunk(stream, path);
            }
            var fn = state.CreateClosure(chunk);
            state.SetTop(1);
            state.Push(new LuaValue(fn));
            state.CallK(0, Constants.LUA_MULTRET);
            return 1;
        }

        public static int tonumber(LuaState state)
        {
            int n = state.GetTop();
            if (n < 1) {
                throw new Exception("insufficient arguments for tonumber");
            }
            double _base = 10;
            if (n > 1) {
                var lv = state.Value(2);
                if (!lv.TryGetNumber(out _base)) {
                    LibUtils.TypeError(2, "tonumber", LuaType.Number, lv.Type);
                }
            }
            LuaValue input = state.Value(1);
            state.Push(tonumber(input, (int)_base));
            return 1;
        }
        
        public static LuaValue tonumber(LuaValue input, int _base  = 10)
        {
            if (input.Type == LuaType.Boolean ||
                input.Type == LuaType.Number)
                return new LuaValue(input.Number());
            if (input.Type != LuaType.String) return new LuaValue(LuaType.Nil);
            if(_base < 1 || _base > 36) LibUtils.ArgError(2, "tonumber", "base out of range");
            if (_base == 10)
            {
                return input.TryGetNumber(out var n) ? new LuaValue(n) : new LuaValue(LuaType.Nil);
            }
            else
            {
                var trimmed = input.ToString().Trim();
                if (trimmed.Length == 0) return new LuaValue(LuaType.Nil);
                bool negate = false;
                int startIdx = 0;
                if (trimmed[0] == '-') { negate = true; startIdx = 1; } //negative
                if (trimmed[0] == '+') { startIdx = 1; } //positive
                double retval = 0;
                for (int i = startIdx; i < trimmed.Length; i++)
                {
                    char ch = trimmed[i];
                    int value;
                    if (ch >= 97) value = ch - 87;
                    else if (ch >= 65) value = ch - 55;
                    else value = ch - 48;
                    if (value < 0 || value >= _base) {
                        return new LuaValue(LuaType.Nil);
                    }
                    retval = retval * _base + value;
                }
                return new LuaValue(negate ? -retval : retval);
            }
        }
        
        public static int type(LuaState state)
        {
            state.Push(state.Value(1).Type.ToString().ToLowerInvariant());
            return 1;
        }

        public static int setmetatable(LuaState state)
        {
            var table = state.Value(1).Table();
            var mt = state.Value(2).Table();
            table.MetaTable = mt;
            state.Push(state.Value(1)); //push the table
            return 1;
        }
        

        public static int getmetatable(LuaState state)
        {
            var v = state.Value(1);
            if (v.Type != LuaType.Table) LibUtils.TypeError(1, "getmetatable", LuaType.Table, v.Type);
            var tab = v.Table().MetaTable;
            if(tab == null)
                state.Push(new LuaValue());
            else
                state.Push(new LuaValue(tab));
            return 1;
        }

        public static int rawget(LuaState state)
        {
            var table = state.Value(1).Table();
            var key = state.Value(2);
            state.Push(table[key]);
            return 1;
        }
        

        private static (string, DelegateClrFunction)[] funcs =
        {
            ("assert", new DelegateClrFunction(assert)),
            ("collectgarbage", new DelegateClrFunction(collectgarbage)),
            ("dofile", new DelegateClrFunction(dofile)),
            ("error", new DelegateClrFunction(error)),
            ("print", new DelegateClrFunction(print)),
            ("tonumber", new DelegateClrFunction(tonumber)),
            ("tostring", new DelegateClrFunction(tostring)),
            ("setmetatable", new DelegateClrFunction(setmetatable)),
            ("getmetatable", new DelegateClrFunction(getmetatable)),
            ("type", new DelegateClrFunction(type)),
            ("ipairs", new DelegateClrFunction(ipairs)),
            ("pairs", new DelegateClrFunction(pairs)),
            ("rawget", new DelegateClrFunction(rawget)),
        };
        
        public static void Register(LuaState state)
        {
            var e = state.Globals;
            state.AddLib("_G", e);
            foreach (var (name, func) in funcs) {
                e[name] = new LuaValue(func);
            }
        }
    }
}