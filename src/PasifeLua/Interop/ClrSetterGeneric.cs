using System;

namespace PasifeLua.Interop
{
    public abstract class ClrSetterGeneric<T>  : ClrSetter
    {
        public sealed override void Set(object self, LuaValue value)
        {
            if(!(self is T s)) throw new InvalidCastException();
            Set(s,value);
        }

        protected abstract void Set(T self, LuaValue value);
    }
}