using System;
using System.Collections.Generic;

namespace PasifeLua.Interop
{
    public class TypeDescriptor
    {
        public Type Type { get; }
        public bool ThrowOnKeyNotFound { get; } = true;
        //
        internal ClrFunction[] TMs = new ClrFunction[(int)TMS.N];
        
        private Dictionary<string,ClrFunction> functions = new Dictionary<string, ClrFunction>();
        private Dictionary<string,ClrGetter> getters = new Dictionary<string, ClrGetter>();
        private Dictionary<string,ClrSetter> setters = new Dictionary<string, ClrSetter>();
        public TypeDescriptor(Type type)
        {
            Type = type;
        }

        public void OperatorEquals(ClrFunction fun) => TMs[(int) TMS.EQ] = fun;
        public void OperatorLessThan(ClrFunction fun) => TMs[(int) TMS.LT] = fun;
        public void OperatorLessThanEquals(ClrFunction fun) => TMs[(int) TMS.LE] = fun;
        public void OperatorAdd(ClrFunction fun) => TMs[(int) TMS.ADD] = fun;
        public void OperatorSub(ClrFunction fun) => TMs[(int) TMS.SUB] = fun;
        public void OperatorMul(ClrFunction fun) => TMs[(int) TMS.MUL] = fun;
        public void OperatorDiv(ClrFunction fun) => TMs[(int) TMS.DIV] = fun;
        public void OperatorMod(ClrFunction fun) => TMs[(int) TMS.MOD] = fun;

        protected void AddFunction(string name, ClrFunction fun) {
            functions.Add(name, fun);
        }

        protected void AddGetter(string name, ClrGetter getter) {
            getters.Add(name, getter);
        }

        protected void AddSetter(string name, ClrSetter setter) {
            setters.Add(name, setter);
        }

        internal LuaValue Get(object t, LuaValue key, bool self)
        {
            if (!key.AsString(out string s)) return new LuaValue();
            if (self)
            {
                if (functions.TryGetValue(s, out var cf)) {
                    return new LuaValue(cf);
                } else if (getters.TryGetValue(s, out var gt)) {
                    return gt.Get(t);
                }
            }
            else
            {
                if (getters.TryGetValue(s, out var gt)) {
                    return gt.Get(t);
                } else if (functions.TryGetValue(s, out var cf)) {
                    return new LuaValue(cf);
                }
            }
            return new LuaValue();
        }

        internal void Set(object t, LuaValue key, LuaValue value)
        {
            if (!value.AsString(out string s))
            {
                if (ThrowOnKeyNotFound) throw new KeyNotFoundException($"{Type} has no setter for key " + key);
                else return;
            }

            if (setters.TryGetValue(s, out var setter)) {
                setter.Set(t, value);
            } else if (ThrowOnKeyNotFound)
            {
                throw new KeyNotFoundException($"{Type} has no setter for key " + s);
            }
        }

    }
}