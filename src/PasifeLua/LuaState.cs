using System;
using System.Collections.Generic;
using System.IO;
using PasifeLua.Bytecode;
using PasifeLua.Interop;
using PasifeLua.Libs;

namespace PasifeLua
{
    public partial class LuaState
    {
        public const string VERSION = "PasifeLua 0.1 (Lua 5.2 compatible)";
        
        private const int LUAI_FIRSTPSEUDOIDX = (-LUAI_MAXSTACK - 1000);
        public const int LUA_REGISTRYINDEX = LUAI_FIRSTPSEUDOIDX;
        public const int LUA_RIDX_GLOBALS = 2;

        private LuaValue luaRegistry;
        public LuaTable Globals;
        public LuaTable Registry;

        private LuaTable loadedPackages;

        public TextWriter StandardOut;
        private LuaTable stringLib;
        public LuaState()
        {
            Registry = new LuaTable();
            Globals = new LuaTable();
            Globals["_VERSION"] = new LuaValue(VERSION);
            Registry[LUA_RIDX_GLOBALS] = new LuaValue(Globals);
            loadedPackages = new LuaTable();
            Registry["_LOADED"] = new LuaValue(LuaType.Table, loadedPackages);
            luaRegistry = new LuaValue(LuaType.Table, Registry);
            StandardOut = Console.Out;
            StackInit();
            BaseLib.Register(this);
            BitLib.Register(this);
            MathLib.Register(this);
            TableLib.Register(this);
            OsLib.Register(this);
            stringLib = StringLib.Register(this);
            IoLib.Register(this);
            PackageLib.Register(this);
            DebugLib.Register(this);
        }

        internal void AddLib(string name, LuaTable table)
        {
            loadedPackages[name] = new LuaValue(table);
            Globals[name] = new LuaValue(table);
        }
        
        public LuaFunction CreateClosure(LuaChunk chunk, LuaTable env = null)
        {
            var closure = new LuaFunction(this, chunk.Prototype, 1);
            closure.UpValues[0] = new UpVal() {
                ValueStore = new LuaValue(LuaType.Table, env ?? Globals)
            };
            return closure;
        }

        public LuaValue CallFunction(LuaValue value, bool ret, params LuaValue[] args)
        {
            if(!(value.obj is ClrFunction || value.obj is LuaFunction))
                throw new ArgumentException("value is not a function");
            var orig = ci;
            var oldtop = top;
            CheckStack(args.Length + 1);
            Push(value);
            for(int i = 0; i < args.Length; i++)
                Push(args[i]);
            try 
            {
                Call(top - args.Length - 1, ret ? 1 : 0, 0);
            }
            catch (Exception e)
            {
                throw new Exception($"{ErrorRecover(orig, oldtop)} {e.Message}", e);
            }
            LuaValue returnVal = new LuaValue();
            if (ret) {
                returnVal = Value(-1);
                Pop(1);
            }
            return returnVal;
        }

        public LuaValue DoString(string code, string src = null)
        {
            var chunk = new LuaChunk(code, src ?? "<chunk>");
            var closure = CreateClosure(chunk);
            return CallFunction(new LuaValue(closure), true);
        }

        internal string ReadLuaModule(string path)
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
            return null;
        }

        string ErrorRecover(CallInfo originalCi, int oldtop)
        {
            var c = ci;
            string retval;
            try
            {
                if (_Stack[c.Func].obj is Interop.ClrFunction) {
                    c = ci.Previous;
                }
                var func = _Stack[c.Func].Object<LuaFunction>();
                int lineNumber = -1;
                if(c.SavedPC >= 0 && c.SavedPC < func.Prototype.LineInfo.Length)
                    lineNumber = func.Prototype.LineInfo[c.SavedPC];
                var source = string.IsNullOrEmpty(func.Prototype.Source) ? "[chunk]" : func.Prototype.Source;
                retval = $"{source}:{lineNumber}";
            }
            catch (Exception)
            {
                retval = "(error getting debug info)";
            }
            CloseUpVals(oldtop);
            ci = originalCi;
            top = oldtop;
            return retval;
        }
     
        List<UpVal> OpenUpVals = new List<UpVal>();

        UpVal FindUpVal(int level)
        {
            for (int i = 0; i < OpenUpVals.Count; i++)
            {
                if (OpenUpVals[i].StackPtr == level)
                    return OpenUpVals[i];
            }
            var v = new UpVal() {State = this, StackPtr = level};
            OpenUpVals.Add(v);
            return v;
        }

        void CloseUpVals(int level)
        {
            for (int i = OpenUpVals.Count - 1; i >= 0; i--)
            {
                if (OpenUpVals[i].StackPtr >= level)
                {
                    OpenUpVals[i].Close();
                    OpenUpVals.RemoveAt(i);
                }
            }
        }

        class CallInfo
        {
            public int Func;
            public int Top;
            public CallInfo Previous;
            public int nresults;
            public int CallStatus;
            public int Base;
            public int SavedPC;
            
        }

        private const int CIST_REENTRY = 1 << 2;
        private const int CIST_TAIL = 1 << 3;
        private CallInfo ci;
        private int nny = 0;

        void Call(int func, int nresults, int allowyield)
        {
            if (allowyield == 0) nny++;
            if (!luaD_precall(func, nresults))
                Execute();
            if (allowyield == 0) nny--;
        }

        void adjustresults(int nres)
        {
            if (nres == Constants.LUA_MULTRET &&
                ci.Top < top) ci.Top = top;
        }
        
        public void CallK(int nargs, int nresults)
        {
            var func = top - (nargs + 1);
            Call(func, nresults, 0);
            adjustresults(nresults);
        }

        internal bool PCallK(int nargs, int nresults, int errhandler)
        {
            var orig = ci;
            var oldtop = top;
            var func = top - (nargs + 1);
            int errIdx = ci.Func + errhandler;
            try
            {
                Call(func, nresults, 0);
                adjustresults(nresults);
                return true;
            }
            catch (Exception e)
            {
                if (errhandler != 0) {
                    var errfunc = _Stack[errIdx];
                    CallFunction(errfunc, false);
                }
                var v = new LuaValue($"{ErrorRecover(orig, oldtop)} {e.Message}");
                _Stack[func] = v;
                return false;
            }
        }
        
    }
}