using System;
using System.Runtime.CompilerServices;
using PasifeLua.Interop;

namespace PasifeLua
{
    public struct LuaValue
    {
        public LuaType Type => type;

        public bool IsNil() => Type == LuaType.Nil;
        
        internal readonly double number;
        internal readonly object obj;
        private readonly LuaType type;
        
        public T Object<T>()
        {
            if (type == LuaType.UserData ||
                type == LuaType.LightUserData ||
                type == LuaType.Function)
                return (T) obj;
            throw new InvalidCastException();
        }

        public double Number()
        {
            if (!TryGetNumber(out var n))
                throw new InvalidCastException();
            return n;
        }
        public bool TryGetNumber(out double n)
        {
            if (type == LuaType.Number || type == LuaType.Boolean)
            {
                n = number;
                return true;
            }
            n = 0;
            if (type == LuaType.String && Utils.luaO_str2d((string)obj, out n))
            {
                return true;
            }
            return false;
        }
        
        public bool Boolean()
        {
            if (type == LuaType.Number ||
                type == LuaType.Boolean)
                return number != 0;
            return obj != null;
        }

        public LuaTable Table()
        {
            if (type == LuaType.Table)
                return (LuaTable) obj;
            throw new InvalidCastException();
        }

        public LuaValue(bool val)
        {
            type = LuaType.Boolean;
            number = val ? 1 : 0;
            obj = null;
        }

        public LuaValue(LuaTable table)
        {
            type = LuaType.Table;
            number = 0;
            obj = table;
        }

        public LuaValue(double number)
        {
            type = LuaType.Number;
            this.number = number;
            obj = null;
        }
                
        public LuaValue(LuaType type, double number)
        {
            this.type = type;
            this.number = number;
            this.obj = null;
        }
    
        public LuaValue(LuaType type, object obj)
        {
            this.type = type;
            this.number = 0;
            this.obj = obj;
        }

        public LuaValue(ClrFunction func)
        {
            this.type = LuaType.LightUserData;
            this.number = 0;
            this.obj = func;
        }

        public LuaValue(LuaFunction func)
        {
            this.type = LuaType.Function;
            this.number = 0;
            this.obj = func;
        }
                
        public LuaValue(LuaType type)
        {
            this.type = type;
            this.number = 0;
            this.obj = null;
        }

        public LuaValue(string str)
        {
            this.type = LuaType.String;
            this.number = 0;
            this.obj = str;
        }

        public bool AsString(out string s)
        {
            if (type == LuaType.Number)
            {
                s = number.ToString();
                return true;
            } 
            else if (type == LuaType.String)
            {
                s = obj.ToString();
                return true;
            }
            s = null;
            return false;
        }
        public override string ToString()
        {
            return ToString(null);
        }
        public string ToString(LuaState state)
        {
            if (type == LuaType.Nil) return "nil";
            if (type == LuaType.Boolean) return number != 0 ? "true" : "false";
            if (type == LuaType.Number) return number.ToString();
            if (state != null && type == LuaType.Table)
            {
                LuaValue tm;
                if (!(tm = TM.GetTMByObj(state, this, TMS.TOSTRING)).IsNil())
                {
                    state.Push(tm);
                    state.Push(this);
                    state.CallK(1, 1);
                    if (!state.Value(-1).AsString(out var str))
                    {
                        throw new Exception("__tostring meta didn't return string");
                    }
                    state.Pop(1);
                    return str;
                }
            }
            return obj.ToString();
        }

        public override int GetHashCode()
        {
            if (Type == LuaType.Nil) return 0;
            if (Type == LuaType.Boolean || Type == LuaType.Number)
                return number.GetHashCode();
            else
                return obj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is LuaValue lv)) {
                return false;
            }
            return this == lv;
        }

        

        public static bool operator ==(LuaValue a, LuaValue b)
        {
            if (a.Type != b.Type) return false;
            if (a.Type == LuaType.Nil) return true;
            if (a.Type == LuaType.Number ||
                a.Type == LuaType.Boolean)
                return a.number == b.number;
            return a.obj.Equals(b.obj);
        }

        public static bool operator !=(LuaValue a, LuaValue b)
        {
            return !(a == b);
        }
        
        public static bool ValuesEqual(LuaValue a, LuaValue b, LuaState state)
        {
            if (state == null) return a == b;
            if (a.Type != b.Type) return false;
            if (a.Type == LuaType.Nil) return true;
            if (a.Type == LuaType.Number ||
                a.Type == LuaType.Boolean)
                return a.number == b.number;
            if (state.CallOrderTM(ref a, ref b, TMS.EQ, out var res)) {
                return res;
            }
            return a.obj.Equals(b.obj);
        }

        public static bool LessThan(LuaState state, LuaValue a, LuaValue b)
        {
            if(a.Type == LuaType.Nil && b.Type == LuaType.Nil) throw new Exception("attempt to compare two nil values");
            if ((a.Type == LuaType.Number || a.Type == LuaType.Boolean) &&
                (b.Type == LuaType.Number || b.Type == LuaType.Boolean))
                return a.number < b.number;
            if (a.Type == LuaType.String && b.Type == LuaType.String)
            {
                var stra = (string) a.obj;
                var strb = (string) b.obj;
                return stra.CompareTo(strb) < 0;
            }
            if (state.CallOrderTM(ref a, ref b, TMS.LT, out var res)) {
                return res;
            }
            throw new Exception("order method");
        }

        public static bool LessEquals(LuaState state, LuaValue a, LuaValue b)
        {
            if(a.Type == LuaType.Nil && b.Type == LuaType.Nil) throw new Exception("attempt to compare two nil values");
            if ((a.Type == LuaType.Number || a.Type == LuaType.Boolean) &&
                (b.Type == LuaType.Number || b.Type == LuaType.Boolean))
                return a.number <= b.number;
            if (a.Type == LuaType.String && b.Type == LuaType.String)
            {
                var stra = (string) a.obj;
                var strb = (string) b.obj;
                return stra.CompareTo(strb) < 0;
            }
            if (state.CallOrderTM(ref a, ref b, TMS.LE, out var res)) {
                return res;
            }
            if (state.CallOrderTM(ref b, ref a, TMS.LT, out res)) {
                return !res;
            }
            throw new Exception("arithmetic error");
        }
        
    }
}