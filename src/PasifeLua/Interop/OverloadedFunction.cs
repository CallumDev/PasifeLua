using System;

namespace PasifeLua.Interop
{
    public sealed class OverloadedFunction : ClrFunction
    {
        public string Name { get; }
        public FunctionOverload[] Overloads { get; }

        public bool Instance { get; }

        static bool TypeCheck(Type type, ref LuaValue val)
        {
            if(type == typeof(int) || 
               type == typeof(uint) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(long) ||
                type == typeof(ulong))
            {
                return val.TryGetNumber(out _);
            } 
            else if (type == typeof(LuaTable))
            {
                return val.Type == LuaType.Table;
            } 
            else if (type == typeof(bool))
            {
                return val.Type == LuaType.Number ||
                       val.Type == LuaType.Boolean;
            } 
            else if (type == typeof(string))
            {
                return val.Type == LuaType.String || val.IsNil();
            }
            else
            {
                return val.Type == LuaType.UserData && (val.IsNil() ||
                       type.IsInstanceOfType(val.obj));
            }
        }
        public override int Run(LuaState state)
        {
            int paramCount = state.GetTop();
            if (Instance) paramCount--;
            if (paramCount < 0) throw new Exception("Instance method needs to be called with ':' syntax or self parameter.");
            if (paramCount == 0) {
                for (int i = 0; i < Overloads.Length; i++)
                {
                    if (Overloads[i].Parameters.Length == 0)
                        return Overloads[i].Run(state);
                }
            }
            else
            {
                int paramOffset = Instance ? 2 : 1; //params start at pos 1 on the stack
                for (int i = 0; i < Overloads.Length; i++)
                {
                    if (Overloads[i].Parameters.Length != paramCount) continue;
                    bool ok = true;
                    for (int j = 0; j < Overloads[i].Parameters.Length; j++)
                    {
                        if (!TypeCheck(Overloads[i].Parameters[j], ref state.Value(j + paramOffset)))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) return Overloads[i].Run(state);
                }
            }
            throw new Exception($"No suitable overload found for function {Name}");
        }

        public OverloadedFunction(string funcName, bool instance, FunctionOverload[] overloads)
        {
            Name = funcName;
            Instance = instance;
            Overloads = overloads;
        }
    }
}