// Disable warnings about XML documentation
#pragma warning disable 1591

//
// This part taken from KopiLua - https://github.com/NLua/KopiLua
//
// =========================================================================================================
//
// Kopi Lua License
// ----------------
// MIT License for KopiLua
// Copyright (c) 2012 LoDC
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial
// portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ===============================================================================
// Lua License
// -----------
// Lua is licensed under the terms of the MIT license reproduced below.
// This means that Lua is free software and can be used for both academic
// and commercial purposes at absolutely no cost.
// For details and rationale, see http://www.lua.org/license.html .
// ===============================================================================
// Copyright (C) 1994-2008 Lua.org, PUC-Rio.
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.ComponentModel;
using System.Text;
using PasifeLua.Interop;
using lua_Integer = System.Int32;
using LUA_INTFRM_T = System.Int64;
using ptrdiff_t = System.Int32;
using UNSIGNED_LUA_INTFRM_T = System.UInt64;
//pasifelua functions
using static PasifeLua.Libs.KopiLuaShim;
using static PasifeLua.Libs.libc;

namespace PasifeLua.Libs
{
    //TODO: This needs some serious refactoring and cleaning up
    internal class KopiLua_StringLib
    {
        public const int LUA_MAXCAPTURES = 32;

        private static ptrdiff_t posrelat(ptrdiff_t pos, uint len)
        {
            /* relative string position: negative means back from end */
            if (pos < 0) pos += (ptrdiff_t)len + 1;
            return (pos >= 0) ? pos : 0;
        }

        /*
        ** {======================================================
        ** PATTERN MATCHING
        ** =======================================================
        */


        public const int CAP_UNFINISHED = (-1);
        public const int CAP_POSITION = (-2);

        public class MatchState
        {

            public MatchState()
            {
                for (int i = 0; i < LUA_MAXCAPTURES; i++)
                    capture[i] = new capture_();
            }

            public int matchdepth; /* control for recursive depth (to avoid C stack overflow) */
            public CharPtr src_init;  /* init of source string */
            public CharPtr src_end;  /* end (`\0') of source string */
            public LuaState L;
            public int level;  /* total number of captures (finished or unfinished) */

            public class capture_
            {
                public CharPtr init;
                public ptrdiff_t len;
            };
            public capture_[] capture = new capture_[LUA_MAXCAPTURES];
        };


        public const int MAXCCALLS = 1000;
        public const char L_ESC = '%';
        public const string SPECIALS = "^$*+?.([%-";


		
		
        private static int check_capture(MatchState ms, int l)
        {
            l -= '1';
            if (l < 0 || l >= ms.level || ms.capture[l].len == CAP_UNFINISHED)
                return LuaLError(ms.L, "invalid capture index {0}", l + 1);
            return l;
        }



        private static int capture_to_close(MatchState ms)
        {
            int level = ms.level;
            for (level--; level >= 0; level--)
                if (ms.capture[level].len == CAP_UNFINISHED) return level;
            return LuaLError(ms.L, "invalid pattern capture");
        }


        private static CharPtr classend(MatchState ms, CharPtr p)
        {
            p = new CharPtr(p);
            char c = p[0];
            p = p.next();
            switch (c)
            {
                case L_ESC:
                {
                    if (p[0] == '\0')
                        LuaLError(ms.L, "malformed pattern (ends with " + LUA_QL("%") + ")");
                    return p + 1;
                }
                case '[':
                {
                    if (p[0] == '^') p = p.next();
                    do
                    {  /* look for a `]' */
                        if (p[0] == '\0')
                            LuaLError(ms.L, "malformed pattern (missing " + LUA_QL("]") + ")");
                        c = p[0];
                        p = p.next();
                        if (c == L_ESC && p[0] != '\0')
                            p = p.next();  /* skip escapes (e.g. `%]') */
                    } while (p[0] != ']');
                    return p + 1;
                }
                default:
                {
                    return p;
                }
            }
        }


        static bool IsGraphicChar(char c)
        {
            return !char.IsControl(c) && !char.IsWhiteSpace(c);
        }

        static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }

        private static int match_class(char c, char cl)
        {
            bool res;
            switch (char.ToLower(cl))
            {
                case 'a': res = char.IsLetter(c); break;
                case 'c': res = char.IsControl(c); break;
                case 'd': res = char.IsDigit(c); break;
                case 'l': res = char.IsLower(c); break;
                case 'p': res = char.IsPunctuation(c); break;
                case 's': res = char.IsWhiteSpace(c); break;
                case 'g': res = IsGraphicChar(c); break;
                case 'u': res = char.IsUpper(c); break;
                case 'w': res = char.IsLetterOrDigit(c); break;
                case 'x': res = IsHexDigit((char)c); break;
                case 'z': res = (c == 0); break;
                default: return (cl == c) ? 1 : 0;
            }
            return (char.IsLower(cl) ? (res ? 1 : 0) : ((!res) ? 1 : 0));
        }



        private static int matchbracketclass(int c, CharPtr p, CharPtr ec)
        {
            int sig = 1;
            if (p[1] == '^')
            {
                sig = 0;
                p = p.next();  /* skip the `^' */
            }
            while ((p = p.next()) < ec)
            {
                if (p == L_ESC)
                {
                    p = p.next();
                    if (match_class((char)c, (char)(p[0])) != 0)
                        return sig;
                }
                else if ((p[1] == '-') && (p + 2 < ec))
                {
                    p += 2;
                    if ((byte)((p[-2])) <= c && (c <= (byte)p[0]))
                        return sig;
                }
                else if ((byte)(p[0]) == c) return sig;
            }
            return (sig == 0) ? 1 : 0;
        }


        private static int singlematch(int c, CharPtr p, CharPtr ep)
        {
            switch (p[0])
            {
                case '.': return 1;  /* matches any char */
                case L_ESC: return match_class((char)c, (char)(p[1]));
                case '[': return matchbracketclass(c, p, ep - 1);
                default: return ((byte)(p[0]) == c) ? 1 : 0;
            }
        }


        private static CharPtr matchbalance(MatchState ms, CharPtr s,
            CharPtr p)
        {
            if ((p[0] == 0) || (p[1] == 0))
                LuaLError(ms.L, "unbalanced pattern");
            if (s[0] != p[0]) return null;
            else
            {
                int b = p[0];
                int e = p[1];
                int cont = 1;
                while ((s = s.next()) < ms.src_end)
                {
                    if (s[0] == e)
                    {
                        if (--cont == 0) return s + 1;
                    }
                    else if (s[0] == b) cont++;
                }
            }
            return null;  /* string ends out of balance */
        }


        private static CharPtr max_expand(MatchState ms, CharPtr s,
            CharPtr p, CharPtr ep)
        {
            ptrdiff_t i = 0;  /* counts maximum expand for item */
            while ((s + i < ms.src_end) && (singlematch((byte)(s[i]), p, ep) != 0))
                i++;
            /* keeps trying to match with the maximum repetitions */
            while (i >= 0)
            {
                CharPtr res = match(ms, (s + i), ep + 1);
                if (res != null) return res;
                i--;  /* else didn't match; reduce 1 repetition to try again */
            }
            return null;
        }


        private static CharPtr min_expand(MatchState ms, CharPtr s,
            CharPtr p, CharPtr ep)
        {
            for (; ; )
            {
                CharPtr res = match(ms, s, ep + 1);
                if (res != null)
                    return res;
                else if ((s < ms.src_end) && (singlematch((byte)(s[0]), p, ep) != 0))
                    s = s.next();  /* try with one more repetition */
                else return null;
            }
        }


        private static CharPtr start_capture(MatchState ms, CharPtr s,
            CharPtr p, int what)
        {
            CharPtr res;
            int level = ms.level;
            if (level >= LUA_MAXCAPTURES) LuaLError(ms.L, "too many captures");
            ms.capture[level].init = s;
            ms.capture[level].len = what;
            ms.level = level + 1;
            if ((res = match(ms, s, p)) == null)  /* match failed? */
                ms.level--;  /* undo capture */
            return res;
        }


        private static CharPtr end_capture(MatchState ms, CharPtr s,
            CharPtr p)
        {
            int l = capture_to_close(ms);
            CharPtr res;
            ms.capture[l].len = s - ms.capture[l].init;  /* close capture */
            if ((res = match(ms, s, p)) == null)  /* match failed? */
                ms.capture[l].len = CAP_UNFINISHED;  /* undo capture */
            return res;
        }


        static bool PtrEqual(CharPtr a, CharPtr b, uint len)
        {
            for(int i = 0; i < len; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }
        private static CharPtr match_capture(MatchState ms, CharPtr s, int l)
        {
            uint len;
            l = check_capture(ms, l);
            len = (uint)ms.capture[l].len;
            if ((uint)(ms.src_end - s) >= len &&
                PtrEqual(ms.capture[l].init, s, len))
                return s + len;
            else return null;
        }



        private static CharPtr match(MatchState ms, CharPtr s, CharPtr p)
        {
            s = new CharPtr(s);
            p = new CharPtr(p);
            if (ms.matchdepth-- == 0)
                LuaLError(ms.L, "pattern too complex");
            init: /* using goto's to optimize tail recursion */
            switch (p[0])
            {
                case '(':
                {  /* start capture */
                    if (p[1] == ')')  /* position capture? */
                        return start_capture(ms, s, p + 2, CAP_POSITION);
                    else
                        return start_capture(ms, s, p + 1, CAP_UNFINISHED);
                }
                case ')':
                {  /* end capture */
                    return end_capture(ms, s, p + 1);
                }
                case L_ESC:
                {
                    switch (p[1])
                    {
                        case 'b':
                        {  /* balanced string? */
                            s = matchbalance(ms, s, p + 2);
                            if (s == null) return null;
                            p += 4; goto init;  /* else return match(ms, s, p+4); */
                        }
                        case 'f':
                        {  /* frontier? */
                            CharPtr ep; char previous;
                            p += 2;
                            if (p[0] != '[')
                                LuaLError(ms.L, "missing " + LUA_QL("[") + " after " +
                                                LUA_QL("%f") + " in pattern");
                            ep = classend(ms, p);  /* points to what is next */
                            previous = (s == ms.src_init) ? '\0' : s[-1];
                            if ((matchbracketclass((byte)(previous), p, ep - 1) != 0) ||
                                (matchbracketclass((byte)(s[0]), p, ep - 1) == 0)) return null;
                            p = ep; goto init;  /* else return match(ms, s, ep); */
                        }
                        default:
                        {
                            if (char.IsDigit((char)(p[1])))
                            {  /* capture results (%0-%9)? */
                                s = match_capture(ms, s, (byte)(p[1]));
                                if (s == null) return null;
                                p += 2; goto init;  /* else return match(ms, s, p+2) */
                            }
                            //ismeretlen hiba miatt lett ide átmásolva
                            {  /* it is a pattern item */
                                CharPtr ep = classend(ms, p);  /* points to what is next */
                                int m = (s < ms.src_end) && (singlematch((byte)(s[0]), p, ep) != 0) ? 1 : 0;
                                switch (ep[0])
                                {
                                    case '?':
                                    {  /* optional */
                                        CharPtr res;
                                        if ((m != 0) && ((res = match(ms, s + 1, ep + 1)) != null))
                                            return res;
                                        p = ep + 1; goto init;  /* else return match(ms, s, ep+1); */
                                    }
                                    case '*':
                                    {  /* 0 or more repetitions */
                                        return max_expand(ms, s, p, ep);
                                    }
                                    case '+':
                                    {  /* 1 or more repetitions */
                                        return ((m != 0) ? max_expand(ms, s + 1, p, ep) : null);
                                    }
                                    case '-':
                                    {  /* 0 or more repetitions (minimum) */
                                        return min_expand(ms, s, p, ep);
                                    }
                                    default:
                                    {
                                        if (m == 0) return null;
                                        s = s.next(); p = ep; goto init;  /* else return match(ms, s+1, ep); */
                                    }
                                }
                            }
                            //goto dflt;  /* case default */
                        }
                    }
                }
                case '\0':
                {  /* end of pattern */
                    return s;  /* match succeeded */
                }
                case '$':
                {
                    if (p[1] == '\0')  /* is the `$' the last char in pattern? */
                        return (s == ms.src_end) ? s : null;  /* check end of string */
                    else goto dflt;
                }
                default:
                    dflt:
                {  /* it is a pattern item */
                    CharPtr ep = classend(ms, p);  /* points to what is next */
                    int m = (s < ms.src_end) && (singlematch((byte)(s[0]), p, ep) != 0) ? 1 : 0;
                    switch (ep[0])
                    {
                        case '?':
                        {  /* optional */
                            CharPtr res;
                            if ((m != 0) && ((res = match(ms, s + 1, ep + 1)) != null))
                                return res;
                            p = ep + 1; goto init;  /* else return match(ms, s, ep+1); */
                        }
                        case '*':
                        {  /* 0 or more repetitions */
                            return max_expand(ms, s, p, ep);
                        }
                        case '+':
                        {  /* 1 or more repetitions */
                            return ((m != 0) ? max_expand(ms, s + 1, p, ep) : null);
                        }
                        case '-':
                        {  /* 0 or more repetitions (minimum) */
                            return min_expand(ms, s, p, ep);
                        }
                        default:
                        {
                            if (m == 0) return null;
                            s = s.next(); p = ep; goto init;  /* else return match(ms, s+1, ep); */
                        }
                    }
                }
            }
        }



        private static CharPtr lmemfind(CharPtr s1, uint l1,
            CharPtr s2, uint l2)
        {
            if (l2 == 0) return s1;  /* empty strings are everywhere */
            else if (l2 > l1) return null;  /* avoids a negative `l1' */
            else
            {
                CharPtr init;  /* to search for a `*s2' inside `s1' */
                l2--;  /* 1st char will be checked by `memchr' */
                l1 = l1 - l2;  /* `s2' cannot be found after that */
                while (l1 > 0 && (init = memchr(s1, s2[0], l1)) != null)
                {
                    init = init.next();   /* 1st char is already checked */
                    if (PtrEqual(init, s2 + 1, l2))
                        return init - 1;
                    else
                    {  /* correct `l1' and `s1' to try again */
                        l1 -= (uint)(init - s1);
                        s1 = init;
                    }
                }
                return null;  /* not found */
            }
        }


        private static void push_onecapture(MatchState ms, int i, CharPtr s,
            CharPtr e)
        {
            if (i >= ms.level)
            {
                if (i == 0)  /* ms.level == 0, too */
                    LuaPushLString(ms.L, s, (uint)(e - s));  /* add whole match */
                else
                    LuaLError(ms.L, "invalid capture index");
            }
            else
            {
                ptrdiff_t l = ms.capture[i].len;
                if (l == CAP_UNFINISHED) LuaLError(ms.L, "unfinished capture");
                if (l == CAP_POSITION)
                    ms.L.Push(new LuaValue(ms.capture[i].init - ms.src_init + 1));
                else
                    LuaPushLString(ms.L, ms.capture[i].init, (uint)l);
            }
        }


        private static int push_captures(MatchState ms, CharPtr s, CharPtr e)
        {
            int i;
            int nlevels = ((ms.level == 0) && (s != null)) ? 1 : ms.level;
            //LuaLCheckStack(ms.L, nlevels, "too many captures");
            for (i = 0; i < nlevels; i++)
                push_onecapture(ms, i, s, e);
            return nlevels;  /* number of strings pushed */
        }


        private static int str_find_aux(LuaState L, int find)
        {
            uint l1, l2;
            CharPtr s = LuaLCheckLString(L, 1, out l1);
            CharPtr p = PatchPattern(LuaLCheckLString(L, 2, out l2));

            ptrdiff_t init = posrelat(LuaLOptInteger(L, 3, 1), l1) - 1;
            if (init < 0) init = 0;
            else if ((uint)(init) > l1) init = (ptrdiff_t)l1;
            if ((find != 0) && ((LuaToBoolean(L, 4) != 0) ||  /* explicit request? */
                                strpbrk(p, SPECIALS) == null))
            {  /* or no special characters? */
                /* do a plain search */
                CharPtr s2 = lmemfind(s + init, (uint)(l1 - init), p, (uint)(l2));
                if (s2 != null)
                {
                    LuaPushInteger(L, s2 - s + 1);
                    LuaPushInteger(L, (int)(s2 - s + l2));
                    return 2;
                }
            }
            else
            {
                MatchState ms = new MatchState();
                int anchor = 0;
                if (p[0] == '^')
                {
                    p = p.next();
                    anchor = 1;
                }
                CharPtr s1 = s + init;
                ms.L = L;
                ms.matchdepth = MAXCCALLS;
                ms.src_init = s;
                ms.src_end = s + l1;
                do
                {
                    CharPtr res;
                    ms.level = 0;
                    // LuaAssert(ms.matchdepth == MAXCCALLS);
                    ms.matchdepth = MAXCCALLS;
                    if ((res = match(ms, s1, p)) != null)
                    {
                        if (find != 0)
                        {
                            LuaPushInteger(L, s1 - s + 1);  /* start */
                            LuaPushInteger(L, res - s);   /* end */
                            return push_captures(ms, null, null) + 2;
                        }
                        else
                            return push_captures(ms, s1, res);
                    }
                } while (((s1 = s1.next()) <= ms.src_end) && (anchor == 0));
            }
            L.Push(new LuaValue());  /* not found */ // nil
            return 1;
        }


        public static int str_find(LuaState L)
        {
            return str_find_aux(L, 1);
        }


        public static int str_match(LuaState L)
        {
            return str_find_aux(L, 0);
        }

        private class GMatchAuxData
        {
            public CharPtr S;
            public CharPtr P;
            public uint LS;
            public uint POS;
        }


        private static int gmatch_aux(LuaState L, GMatchAuxData auxdata)
        {
            MatchState ms = new MatchState();
            uint ls = auxdata.LS;
            CharPtr s = auxdata.S;
            CharPtr p = auxdata.P;
            CharPtr src;
            ms.L = L;
            ms.matchdepth = MAXCCALLS;
            ms.src_init = s;
            ms.src_end = s + ls;
            for (src = s + auxdata.POS;
                src <= ms.src_end;
                src = src.next())
            {
                CharPtr e;
                ms.level = 0;
                //LuaAssert(ms.matchdepth == MAXCCALLS);
                ms.matchdepth = MAXCCALLS;

                if ((e = match(ms, src, p)) != null)
                {
                    lua_Integer newstart = e - s;
                    if (e == src) newstart++;  /* empty match? go at least one position */
                    auxdata.POS = (uint)newstart;
                    return push_captures(ms, src, e);
                }
            }
            return 0;  /* not found */
        }
        
        public static int str_gmatch(LuaState L)
        {
            string s = LibUtils.GetString(L, 1, "gmatch");
            string p = PatchPattern(LibUtils.GetString(L, 2, "gmatch"));


            var auxdata = new GMatchAuxData()
            {
                S = new CharPtr(s),
                P = new CharPtr(p),
                LS = (uint)s.Length,
                POS = 0
            };
            L.Push(new LuaValue(new DelegateClrFunction((L) => gmatch_aux(L, auxdata))));
            return 1;
        }
        

        private static void add_s(MatchState ms, StringBuilder b, CharPtr s, CharPtr e)
        {
            var str = LibUtils.GetString(ms.L, 3, "string.gsub");
            CharPtr news = str;
            for (int i = 0; i < str.Length; i++)
            {
                if (news[i] != L_ESC)
                    b.Append(news[i]);
                else
                {
                    i++; /* skip ESC */
                    if (!char.IsDigit((char) (news[i])))
                    {
                        if (news[i] != L_ESC)
                        {
                            LuaLError(ms.L, "invalid use of '%' in replacement string");
                        }
                        b.Append(news[i]);
                    }
                    else if (news[i] == '0')
                        b.Append(s.ToString(e - s));
                    else
                    {
                        push_onecapture(ms, news[i] - '1', s, e);
                        LuaLAddValue(ms.L, b); /* add capture to accumulated result */
                    }
                }
            }
        }




        private static void add_value(MatchState ms, StringBuilder b, CharPtr s,
            CharPtr e)
        {
            LuaState L = ms.L;
            switch (LuaType(L, 3))
            {
                case LUA_TNUMBER:
                case LUA_TSTRING:
                {
                    add_s(ms, b, s, e);
                    return;
                }
                // case LUA_TUSERDATA: /// +++ does this make sense ??
                case LUA_TFUNCTION:
                case (int)LuaType.LightUserData:
                {
                    int n;
                    L.Push(L.Value(3));
                    n = push_captures(ms, s, e);
                    L.CallK(n, 1);
                    break;
                }
                case LUA_TTABLE:
                {
                    push_onecapture(ms, 0, s, e);
                    var k = L.Value(-1);
                    L.Pop(1);
                    var tab = L.Value(3).Table();
                    // DEBT: this should call metamethods, now it performs raw access
                    L.Push(tab.GetValue(k));
                    //LuaGetTable(L, 3);
                    break;
                }
            }
            if (LuaToBoolean(L, -1) == 0)
            {  /* nil or false? */
                L.Pop(1);
                LuaPushLString(L, s, (uint)(e - s));  /* keep original text */
            }
            else if (!L.Value(-1).AsString(out _))
                LuaLError(L, "invalid replacement value (a {0})", L.Value(-1).Type.ToString().ToLower());

            LuaLAddValue(L, b);  /* add result to accumulator */
        }


        public static int str_gsub(LuaState L)
        {
            uint srcl;
            CharPtr src = LuaLCheckLString(L, 1, out srcl);
            CharPtr p = PatchPattern(LibUtils.GetString(L, 2, "gsub"));
            int tr = LuaType(L, 3);
            int max_s = LuaLOptInt(L, 4, (int)(srcl + 1));
            int anchor = 0;
            if (p[0] == '^')
            {
                p = p.next();
                anchor = 1;
            }
            int n = 0;
            MatchState ms = new MatchState();
            var b = new StringBuilder();
            LibUtils.ArgCheck( tr == LUA_TNUMBER || tr == LUA_TSTRING ||
                            tr == LUA_TFUNCTION || tr == LUA_TTABLE ||
                            tr == LUA_TUSERDATA || tr == (int)LuaType.LightUserData, 3, "string.gsub",
                "string/function/table expected");
            //LuaLBuffInit(L, b);
            ms.L = L;
            ms.matchdepth = MAXCCALLS;
            ms.src_init = src;
            ms.src_end = src + srcl;
            while (n < max_s)
            {
                CharPtr e;
                ms.level = 0;
                //LuaAssert(ms.matchdepth == MAXCCALLS);
                ms.matchdepth = MAXCCALLS;
                e = match(ms, src, p);
                if (e != null)
                {
                    n++;
                    add_value(ms, b, src, e);
                }
                if ((e != null) && e > src) /* non empty match? */
                    src = e;  /* skip it */
                else if (src < ms.src_end)
                {
                    char c = src[0];
                    src = src.next();
                    b.Append(c);
                }
                else break;
                if (anchor != 0) break;
            }
            b.Append(src.ToString(ms.src_end - src));
            L.Push(new LuaValue(b.ToString()));
            LuaPushInteger(L, n);  /* number of substitutions */
            return 2;
        }

        private static string PatchPattern(string charPtr)
        {
            return charPtr.Replace("\0", "%z");
        }
    }
}