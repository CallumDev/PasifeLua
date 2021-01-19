using System;

namespace PasifeLua.Interop
{
    public abstract class ClrGetterGeneric<T>  : ClrGetter
    {
        public sealed override LuaValue Get(object self)
        {
            if(!(self is T s)) throw new InvalidCastException();
            return Get(s);
        }

        protected abstract LuaValue Get(T self);
    }
}