using System;
using System.Collections;
using System.Collections.Generic;

namespace PasifeLua.Interop
{
    static class IListInterop
    {
        static readonly Dictionary<string, ClrFunction> FunctionsGeneric = new Dictionary<string, ClrFunction>()
        {
            { "Add", new DelegateClrFunction(Add) },
            { "Clear", new DelegateClrFunction(Clear) },
            { "Contains", new DelegateClrFunction(Contains) },
            { "IndexOf", new DelegateClrFunction(IndexOf) }, 
            { "Remove", new DelegateClrFunction(Remove) },
            { "RemoveAt", new DelegateClrFunction(RemoveAt) }
        };

        public static int Add(LuaState state)
        {
            var list = state.Value(1).Object<IList>();
            state.Push(new LuaValue(list.Add(state.Value(2).Value)));
            return 1;
        }

        public static int Clear(LuaState state)
        {
            state.Value(1).Object<IList>().Clear();
            return 0;
        }
        
        public static int Contains(LuaState state)
        {
            var list = state.Value(1).Object<IList>();
            state.Push(new LuaValue(list.Contains(state.Value(2).Value)));
            return 1;
        }
        
        public static int IndexOf(LuaState state)
        {
            var list = state.Value(1).Object<IList>();
            state.Push(new LuaValue(list.IndexOf(state.Value(2).Value)));
            return 1;
        }
        
        public static int Insert(LuaState state)
        {
            if (!state.Value(2).TryGetNumber(out double n))
                throw new InvalidCastException();
            state.Value(1).Object<IList>().Insert((int) n, state.Value(3).Value);
            return 0;
        }
        
        public static int Remove(LuaState state)
        {
            state.Value(1).Object<IList>().Remove(state.Value(2).Value);
            return 0;
        }

        public static int RemoveAt(LuaState state)
        {
            if (!state.Value(2).TryGetNumber(out double n))
                throw new InvalidCastException();
            state.Value(1).Object<IList>().RemoveAt((int)n);
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