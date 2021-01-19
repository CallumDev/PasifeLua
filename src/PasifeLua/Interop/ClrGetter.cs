namespace PasifeLua.Interop
{
    public abstract class ClrGetter
    {
        public abstract LuaValue Get(object self);
    }
}