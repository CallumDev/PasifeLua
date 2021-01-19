namespace PasifeLua
{
    public enum LuaType
    {
        Nil = 0,
        Boolean = 1,
        LightUserData = 2,
        Number = 3,
        String = 4,
        Table = 5,
        Function = 6,
        UserData = 7,
    }

    public static class LuaTypeExtensions
    {
        static readonly string[] _luanames =
        {
            "nil", "boolean", "lightuserdata", "number", "string", "table", "function", "userdata"
        };
        public static string LuaName(this LuaType type)
        {
            return _luanames[(int) type];
        }
    }
}