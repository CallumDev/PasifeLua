using System.Text;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public class TableLib
    {
        public static int remove(LuaState state)
        {
            var tab = state.Value(1).Table();
            if (state.GetTop() == 2)
                LuaTable.remove(tab, (int)state.Value(2).Number());
            else 
                LuaTable.remove(tab); 
            return 0;
        }

        public static int unpack(LuaState state)
        {
            var tab = state.Value(1).Table();
            int i = 0;
            while (true) {
                var val = tab[1 + i];
                if (val.IsNil()) break;
                state.Push(val);
                i++;
            }
            return i;
        }

        public static int concat(LuaState state)
        {
            var builder = new StringBuilder();
            var tab = state.Value(1).Table();
            string sep = null;
            int start = -1;
            int end = int.MaxValue;
            int n = state.GetTop();
            if (n > 1) sep = LibUtils.GetString(state, 2, "concat");
            if (n > 2) start = LibUtils.GetSigned(state, 3, "concat");
            if (n > 3) end = LibUtils.GetSigned(state, 4, "concat");
            int k = 0;
            if (start > 0)
            {
                if (end < int.MaxValue) //start to end
                {
                    for (int i = start; i <= end; i++)
                    {
                        if(!tab[i].AsString(out var s))
                            LibUtils.ArgError(1, "concat", "table contains non-strings");
                        if (!string.IsNullOrEmpty(sep) && i > start) builder.Append(sep);
                        builder.Append(s);
                    }
                }
                else //go from start index
                {
                    LuaValue v;
                    while (!(v = tab[start]).IsNil())
                    {
                        if(!v.AsString(out var s))
                            LibUtils.ArgError(1, "concat", "table contains non-strings");
                        if (!string.IsNullOrEmpty(sep) && k > 0) builder.Append(sep);
                        builder.Append(s);
                        start++;
                        k++;
                    }
                }
            }
            else //concat all items
            {
                foreach (var item in tab)
                {
                    if (!item.Value.AsString(out var s))
                        LibUtils.ArgError(1, "concat", "table contains non-strings");
                    if (!string.IsNullOrEmpty(sep) && k > 0) builder.Append(sep);
                    builder.Append(s);
                    k++;
                }
            }

            state.Push(new LuaValue(builder.ToString()));
            return 1;
        }

        private static readonly (string, DelegateClrFunction)[] funcs =
        {
            ("remove", new DelegateClrFunction(remove)),
            ("unpack", new DelegateClrFunction(unpack)),
            ("concat", new DelegateClrFunction(concat))
        };
        public static void Register(LuaState state)
        {
            var table = LibUtils.CreateLib(state, "table", funcs);
            //set global unpack for Lua 5.1 compat
            state.Globals["unpack"] = table["unpack"];
        }
    }
}