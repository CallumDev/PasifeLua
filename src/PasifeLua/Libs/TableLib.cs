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

        private static readonly (string, DelegateClrFunction)[] funcs =
        {
            ("remove", new DelegateClrFunction(remove)),
            ("unpack", new DelegateClrFunction(unpack))
        };
        public static void Register(LuaState state)
        {
            var table = LibUtils.CreateLib(state, "table", funcs);
            //set global unpack for Lua 5.1 compat
            state.Globals["unpack"] = table["unpack"];
        }
    }
}