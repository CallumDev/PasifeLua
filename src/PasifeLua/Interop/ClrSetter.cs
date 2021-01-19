namespace PasifeLua.Interop
{
    public abstract class ClrSetter
    {
        public abstract void Set(object self, LuaValue value);
    }
}