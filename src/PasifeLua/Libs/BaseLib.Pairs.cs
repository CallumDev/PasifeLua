using System;
using System.Collections;
using System.Collections.Generic;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static partial class BaseLib
    {
        static int pairsimpl(LuaState s)
        {
            var tab = s.Value(1).Table();
            var k = s.Value(2);
            if (!(k = tab.NextKey(k)).IsNil())
            {
                s.Push(k);
                s.Push(tab[k]);
                return 2;
            } else {
                s.Push(new LuaValue(LuaType.Nil));
                s.Push(new LuaValue(LuaType.Nil));
                return 1;
            }
        }

        static int pairsdict_impl(LuaState s, IDictionaryEnumerator it)
        {
            if (it.MoveNext())
            {
                s.Push(LuaValue.FromObject(it.Key));
                s.Push(LuaValue.FromObject(it.Value));
                return 2;
            } else {
                s.Push(new LuaValue(LuaType.Nil));
                s.Push(new LuaValue(LuaType.Nil));
                return 1;
            }
        }

        private static DelegateClrFunction pairsDel = new DelegateClrFunction(pairsimpl);

        public static int pairs(LuaState state)
        {
            if (state.Value(1).Type == LuaType.Table)
            {
                var tab = state.Value(1).Table();
                state.Push(new LuaValue(pairsDel));
            } 
            else if (state.Value(1).Type == LuaType.UserData)
            {
                var obj = state.Value(1).obj;
                if (obj is IDictionary dict)
                {
                    var it = dict.GetEnumerator();
                    state.Push(new LuaValue(new DelegateClrFunction(s => { return pairsdict_impl(s, it); })));
                } else if (obj is IList) {
                    return ipairs(state);
                }
                else {
                    throw new Exception("cannot iterate on type");
                }
            }
            else
            {
                throw new Exception("cannot iterate on type");
            }
            state.Push(state.Value(1));
            state.Push(LuaValue.Nil);
            return 3;
        }
    }
}