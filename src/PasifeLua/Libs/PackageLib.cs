namespace PasifeLua.Libs
{
    public static class PackageLib
    {
        public static void Register(LuaState state)
        {
            var package = LibUtils.CreateLib(state, "package");
            package["loaded"] = new LuaValue(state.Registry["_LOADED"].Table());
        }
    }
}