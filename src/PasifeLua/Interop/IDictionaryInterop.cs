using System;
using System.Collections;
using System.Collections.Generic;

namespace PasifeLua.Interop
{
    static class IDictionaryInterop
    {
        static readonly Dictionary<string, ClrFunction> FunctionsGeneric = new Dictionary<string, ClrFunction>()
        {
            { "Add", new DelegateClrFunction(Add) },
            { "Clear", new DelegateClrFunction(Clear) },
            { "Contains", new DelegateClrFunction(Contains) },
            { "Remove", new DelegateClrFunction(Remove) },
        };
        
        public static int Add(LuaState state)
        {
            var dict = state.Value(1).Object<IDictionary>();
            var key = state.Value(2).Value;
            var value = state.Value(3).Value;
            dict.Add(key, value);
            return 0;
        }

        public static int Clear(LuaState state)
        {
            var dict = state.Value(1).Object<IDictionary>();
            dict.Clear();
            return 0;
        }

        public static int Contains(LuaState state)
        {
            var dict = state.Value(1).Object<IDictionary>();
            var key = state.Value(2).Value;
            state.Push(new LuaValue(dict.Contains(key)));
            return 1;
        }

        public static int Remove(LuaState state)
        {
            var dict = state.Value(1).Object<IDictionary>();
            var key = state.Value(2).Value;
            dict.Remove(key);
            return 0;
        }
        
        static LuaValue Get(Dictionary<string, ClrFunction> dict, string k)
        {
            if (dict.TryGetValue(k, out var func)) return new LuaValue(func);
            return new LuaValue();
        }
        
        public static LuaValue GetFunction(LuaValue key)
        {
            if (!key.AsString(out string s)) return new LuaValue();
            return Get(FunctionsGeneric, s);
        }
    }
}