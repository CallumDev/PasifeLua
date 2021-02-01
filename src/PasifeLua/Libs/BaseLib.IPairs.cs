using System;
using System.Collections;
using System.Collections.Generic;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static partial class BaseLib
    {
        //lua implementation
        static int ipairsimpl(LuaState s)
        {
            var tab = s.Value(1).Table();
            var i = (int)s.Value(2).Number();
            i++;
            s.Push(i);
            var x = tab[i];
            s.Push(x);
            if (x.IsNil()) return 1;
            else return 2;
        }
        static DelegateClrFunction ipairsDel = new DelegateClrFunction(ipairsimpl);
        
        //backing for type-specific implementation
        static int ipairs_array<T>(LuaState s, Func<T,LuaValue> conv)
        {
            var arr = s.Value(1).Object<IList<T>>();
            var i = (int) s.Value(2).Number();
            i++;
            s.Push(i);
            if ((i - 1) >= arr.Count) {
                s.Push(new LuaValue());
                return 1;
            }
            else {
                s.Push(conv(arr[i - 1]));
            }
            return 2;
        }
        
        //generic implementation
        static int ipairs_IListImpl(LuaState s)
        {
            var arr = s.Value(1).Object<IList>();
            var i = (int) s.Value(2).Number();
            i++;
            s.Push(i);
            if ((i - 1) >= arr.Count) {
                s.Push(new LuaValue());
                return 1;
            }
            else {
                s.Push(LuaValue.FromObject(arr[i - 1]));
            }
            return 2;
        }

        private static DelegateClrFunction ipairsIList = new DelegateClrFunction(ipairs_IListImpl);
        static int ipairs_stringsImpl(LuaState s) => ipairs_array<string>(s, x => new LuaValue(x));
        static int ipairs_doubleImpl(LuaState s) => ipairs_array<double>(s, x => new LuaValue(x));
        static int ipairs_floatImpl(LuaState s) => ipairs_array<float>(s, x => new LuaValue(x));
        static int ipairs_intImpl(LuaState s) => ipairs_array<int>(s, x => new LuaValue(x));


        private static DelegateClrFunction ipairsStrings = new DelegateClrFunction(ipairs_stringsImpl);
        private static DelegateClrFunction ipairsDouble = new DelegateClrFunction(ipairs_doubleImpl);
        private static DelegateClrFunction ipairsFloat = new DelegateClrFunction(ipairs_floatImpl);
        private static DelegateClrFunction ipairsInt = new DelegateClrFunction(ipairs_intImpl);
        
        public static int ipairs(LuaState state)
        {
            var val = state.Value(1);
            if (val.IsNil()) throw new Exception("attempt to iterate on nil value");
            if (val.Type == LuaType.Table) {
                var tab = state.Value(1).Table();
                //generator
                state.Push(new LuaValue(ipairsDel));
            }
            else
            {
                var obj = val.obj;
                if (obj is IList<string>)
                    state.Push(new LuaValue(ipairsStrings));
                else if (obj is IList<double>)
                    state.Push(new LuaValue(ipairsDouble));
                else if (obj is IList<float>)
                    state.Push(new LuaValue(ipairsFloat));
                else if (obj is IList<int>)
                    state.Push(new LuaValue(ipairsInt));
                else if (obj is IList)
                    state.Push(new LuaValue(ipairsIList));
                else
                {
                    throw new Exception($"cannot iterate on type {obj.GetType()}");
                }
            }
            //state
            state.Push(state.Value(1));
            //initial value
            state.Push(0);
            return 3;
        }
    }
}