using System;
using System.Text;
using LT = PasifeLua.LuaType;
namespace PasifeLua.Libs
{
    //Methods used in KopiLua_StrLib
    //TODO: Eventually this class should not exist
    static class KopiLuaShim
    {
        public const int LUA_TNUMBER = (int) LT.Number;
        public const int LUA_TSTRING = (int) LT.String;
        public const int LUA_TFUNCTION = (int) LT.Function;
        public const int LUA_TTABLE = (int) LT.Table;
        public const int LUA_TUSERDATA = (int) LT.UserData;
        public static int LuaType(LuaState L, int pos) =>  (int) L.Value(pos).Type;
        public static int LuaLError(LuaState L, string fmt, params object[] pm)=> throw new Exception(string.Format(fmt, pm));

        public static string LUA_QL(string s) => $"'{s}'";
        
        public static CharPtr memchr(CharPtr ptr, char c, uint count)
        {
            for (uint i = 0; i < count; i++)
                if (ptr[i] == c)
                    return new CharPtr(ptr.chars, (int)(ptr.index + i));
            return null;
        }
        
        public static CharPtr strpbrk(CharPtr str, CharPtr charset)
        {
            for (int i = 0; str[i] != '\0'; i++)
            for (int j = 0; charset[j] != '\0'; j++)
                if (str[i] == charset[j])
                    return new CharPtr(str.chars, str.index + i);
            return null;
        }

        public static void LuaPushLString(LuaState L, CharPtr s, uint len)
        {
            string ss = s.ToString((int) len);
            L.Push(new LuaValue(ss));
        }
        
        public static void LuaLAddValue(LuaState l, StringBuilder b)
        {
            var v = l.Value(-1);
            l.Pop(1);
            b.Append(v.ToString());
        }

        public static int LuaLOptInt(LuaState L, int pos, int def) => LuaLOptInteger(L, pos, def);
        public static int LuaLOptInteger(LuaState L, int pos, int def)
        {
            if (L.GetTop() >= pos)
            {
                if (L.Value(pos).TryGetNumber(out double n))
                    return (int) n;
            }
            return def;
        }
        
        public static void LuaPushInteger(LuaState L, int i) => L.Push(new LuaValue(i));
        public static int LuaToBoolean(LuaState l, int p) => l.Value(p).Boolean() ? 1 : 0;


        public static string LuaLCheckLString(LuaState L, int argnumber, out uint l)
        {
            var str = LibUtils.GetString(L, argnumber, "");
            l = (uint)str.Length;
            return str;
        }
    }
}