using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static class StringLib
    {
        //FORMAT implementation
        private const char L_ESC = '%';
        private const string FLAGS = "-+ #0";
        static void scanformat(string strfmt, ref int sp, out string form)
        {
            int p = sp;
            while ((p < strfmt.Length) && FLAGS.IndexOf(strfmt[p]) != -1) p++;
            if(p - sp >= FLAGS.Length)
                throw new Exception("invalid format (repeated flags)");
            if (char.IsDigit(strfmt[p])) p++; /* skip width */
            if (char.IsDigit(strfmt[p])) p++; /* 2 digits at most */
            if (strfmt[p] == '.')
            {
                p++;
                if (char.IsDigit(strfmt[p])) p++; /* skip precision */
                if (char.IsDigit(strfmt[p])) p++; /* (2 digits at most) */
            }
            if(char.IsDigit(strfmt[p]))
                throw new Exception("invalid format (width or precision too long)");
            var sb = new StringBuilder();
            sb.Append('%');
            for (int k = sp; k <= p; k++)
                sb.Append(strfmt[k]);
            form = sb.ToString();
            sp = p;
        }

        static string MakeLiteral(string s)
        {
            if (s == null || s.Length == 0) {
                return "\"\"";
            }
            char         c = '\0';
            StringBuilder sb = new StringBuilder(s.Length + 4);
            sb.Append('"');
            String     t;
            for (int i = 0; i < s.Length; i++) {
                c = s[i];
                switch (c) {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\0':
                        sb.Append("\\000");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int) c).ToString("X4"));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static int format(LuaState state)
        {
            int top = state.GetTop();
            int arg = 1;
            
            StringBuilder buf = new StringBuilder();
            var strfmt = LibUtils.GetString(state, 1, "format");
            int sp = 0;
            while (sp < strfmt.Length)
            {
                if (strfmt[sp] != L_ESC)
                    buf.Append(strfmt[sp++]);
                else if ((sp + 1) >= strfmt.Length) {
                    throw new Exception("incomplete format specifier");
                } else if(strfmt[sp + 1] == L_ESC) {
                    buf.Append('%');
                    sp += 2;
                } else
                {
                    sp++;
                    string form;
                    if(++arg > top)
                        throw new Exception("no value");
                    scanformat(strfmt, ref sp, out form);
                    switch (strfmt[sp++]) {
                        case 'c': case 'd': case 'i':
                            buf.Append(libc.sprintf(form, (int) state.Value(arg).Number()));
                            break;
                        case 'o': case 'u': case 'x': case 'X':
                            buf.Append(libc.sprintf(form, (uint)state.Value(arg).Number()));
                            break;
                        case 'q':
                            buf.Append(MakeLiteral(state.Value(arg).ToString()));
                            break;
                        case 'e':
                        case 'E':
                        case 'f':
                        case 'g':
                        case 'G': {
                            buf.Append(libc.sprintf(form, state.Value(arg).Number()));
                            break;
                        }
                        case 's':
                        {
                            buf.Append(libc.sprintf(form, state.Value(arg).ToString()));
                            break;
                        }
                        default:
                            throw new Exception($"invalid format {form}");
                    }
                }
            }
            state.Push(buf.ToString());
            return 1;
        }
        
        //reverse implementation
        //Unicode correct reverse, not Lua accurate but better™ 
        static IEnumerable<string> GraphemeClusters(this string s) {
            var enumerator = StringInfo.GetTextElementEnumerator(s);
            while(enumerator.MoveNext()) {
                yield return (string)enumerator.Current;
            }
        }
        static string ReverseGraphemeClusters(this string s) {
            return string.Join("", s.GraphemeClusters().Reverse().ToArray());
        }
        
        public static int reverse(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "reverse");
            state.Push(ReverseGraphemeClusters(s));
            return 1;
        }

        public static int rep(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "rep");
            var n = (int)LibUtils.GetNumber(state, 2, "rep");
            //fast path for small N, avoid allocs
            if (n <= 0) {
                state.Push("");
            } else if (n == 1) {
                state.Push(s);
            } else if (n == 2) {
                state.Push(s + s);
            } else {
                //generic stringbuilder
                state.Push(new StringBuilder(s.Length * n).Insert(0, s, n).ToString());
            }
            return 1;
        }

        public static int upper(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "upper");
            state.Push(s.ToUpper());
            return 1;
        }

        public static int lower(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "lower");
            state.Push(s.ToLower());
            return 1;
        }

        public static int len(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "len");
            state.Push(s.Length);
            return 1;
        }

        static int Index(string s, int j)
        {
            return (j < 0) ? s.Length + j : j - 1;
        }
        public static int lbyte(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "byte");
            var n = state.GetTop();
            int startIdx = 1;
            int finishIdx;
            if (state.GetTop() > 1) {
                startIdx = (int) LibUtils.GetNumber(state, 2, "byte");
            }
            if (state.GetTop() > 2) {
                finishIdx = (int) LibUtils.GetNumber(state, 3, "byte");
            }
            else {
                finishIdx = startIdx;
            }
            int k = 0;
            for (int j = startIdx; j <= finishIdx; j++)
            {
                var idx = Index(s, j);
                if (idx >= 0 && idx < s.Length)
                {
                    state.Push((double) s[idx]);
                    k++;
                }
            }
            return k;
        }

        public static int lchar(LuaState state)
        {
            StringBuilder builder = new StringBuilder();
            int n = state.GetTop();
            for (int i = 1; i <= n; i++)
            {
                builder.Append((char) LibUtils.GetNumber(state, 1, "char"));
            }
            state.Push(builder.ToString());
            return 1;
        }

        public static int sub(LuaState state)
        {
            var s = LibUtils.GetString(state, 1, "sub");
            var start = (int)LibUtils.GetNumber(state, 2, "sub");
            int end = s.Length;
            if (state.GetTop() > 2)
            {
                end = (int)LibUtils.GetNumber(state, 3, "sub");
            }
            var sIdx = Index(s, start);
            var eIdx = Index(s, end);
            var len = (eIdx - sIdx) + 1;
            if (len <= 0)
                state.Push("");
            else
                state.Push(s.Substring(sIdx, len));
            return 1;
        }
        
        private static readonly (string, DelegateClrFunction)[] funcs =
        {
            ("byte", new DelegateClrFunction(lbyte)),
            ("char", new DelegateClrFunction(lchar)),
            ("upper", new DelegateClrFunction(upper)),
            ("lower", new DelegateClrFunction(lower)),
            ("len", new DelegateClrFunction(len)),
            ("format", new DelegateClrFunction(format)),
            ("reverse", new DelegateClrFunction(reverse)),
            ("rep", new DelegateClrFunction(rep)),
            ("sub", new DelegateClrFunction(sub)),
            //kopilua functions
            ("find", new DelegateClrFunction(KopiLua_StringLib.str_find)),
            ("match", new DelegateClrFunction(KopiLua_StringLib.str_match)),
            ("gsub", new DelegateClrFunction(KopiLua_StringLib.str_gsub)),
            ("gmatch", new DelegateClrFunction(KopiLua_StringLib.str_gmatch))
        };
        public static LuaTable Register(LuaState state)
        {
            return LibUtils.CreateLib(state, "string", funcs);
        }
    }
}