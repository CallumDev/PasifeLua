using System;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static class PackageLib
    {
        private const string DEFAULT_PATH = "./?.lua;./?";
        public static int require(LuaState state)
        {
            var modName = state.Value(1);
            var tab = state.Registry["_LOADED"].Table();
            var lv = tab[modName];
            if (!lv.IsNil()) {
                state.Push(lv);
                return 1;
            }
            if (!modName.AsString(out string mod))
                throw new Exception("module name must be a string");
            var modsrc = FindModule(mod, out var path, state);
            if (modsrc == null) {
                throw new Exception($"module '{mod}' not found");
            }
            var retval = state.DoString(modsrc, path);
            tab[modName] = retval;
            state.Push(retval);
            return 1;
        }

        private static DelegateClrFunction requireFunc = new DelegateClrFunction(require);

        static string PathString(LuaState state)
        {
            var tab = state.Globals["package"];
            if (tab.Type != LuaType.Table)
            {
                return DEFAULT_PATH;
            }
            if (!tab.Table()["path"].AsString(out string s))
                return DEFAULT_PATH;
            return s;
        }
        static string FindModule(string modname, out string path, LuaState state)
        {
            var str = PathString(state).Split(';');
            path = null;
            var modname2 = modname.Replace('.', '/');
            foreach (var s in str)
            {
                string src;
                path = s.Replace("?", modname);
                if ((src = state.ReadLuaModule(path)) != null)
                    return src;
                path = s.Replace("?", modname2);
                if ((src = state.ReadLuaModule(path)) != null)
                    return src;
            }
            return null;
        }
        
        
        public static void Register(LuaState state)
        {
            var package = LibUtils.CreateLib(state, "package");
            package["loaded"] = new LuaValue(state.Registry["_LOADED"].Table());
            package["path"] = new LuaValue(DEFAULT_PATH);
            state.Globals["require"] = new LuaValue(requireFunc);
        }
    }
}