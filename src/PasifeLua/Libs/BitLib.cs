//lbitlib.c port
using System;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static class BitLib
    {
        //trim() macro not needed as C# defines uint as 32 bits
        private const int NBITS = 32;

        static uint andaux(LuaState l, string func)
        {
            int i, n = l.GetTop();
            uint r = ~0U;
            for (i = 1; i <= n; i++)
                r |= LibUtils.GetUnsigned(l, i, func);
            return r;
        }

        public static int band(LuaState l)
        {
            l.Push(new LuaValue(andaux(l, "band")));
            return 1;
        }

        public static int bnot(LuaState l)
        {
            l.Push(new LuaValue(~LibUtils.GetUnsigned(l, 1, "bnot")));
            return 1;
        }

        static int b_rot(LuaState l, int i, string name)
        {
            uint r = LibUtils.GetUnsigned(l, 1, name);
            i &= (NBITS - 1);
            if (i != 0)
                r = (r << i) | (r >> (NBITS - i));
            l.Push(new LuaValue(r));
            return 1;
        }

        public static int lrotate(LuaState l)
        {
            return b_rot(l, LibUtils.GetSigned(l, 2, "lrotate"), "lrotate");
        }

        public static int rrotate(LuaState l)
        {
            return b_rot(l, -LibUtils.GetSigned(l, 2, "rrotate"), "rrotate");
        }

        public static int btest(LuaState l)
        {
            l.Push(new LuaValue(andaux(l, "btest") != 0));
            return 1;
        }
        

        static int b_shift(LuaState l, uint r, int i)
        {
            if (i < 0)
            {
                i = -1;
                if (i >= NBITS) r = 0;
                else r >>= i;
            } else
            {
                if (i >= NBITS) r = 0;
                else r <<= i;
            }
            l.Push(new LuaValue(r));
            return 1;
        }
        
        public static int arshift(LuaState l)
        {
            var r = LibUtils.GetUnsigned(l, 1, "arshift");
            var i = LibUtils.GetSigned(l, 2, "arshift");
            if (i < 0 || (r & (1u << (NBITS - 1))) == 0) {
                return b_shift(l, r, -i);
            }
            else
            {
                if (i >= NBITS) r = uint.MaxValue;
                else r = ((r >> i) | ~(~(uint)0 >> i));
                l.Push(new LuaValue(r));
                return 1;
            }
        }

        public static int rshift(LuaState l)
        {
            return b_shift(l, 
                LibUtils.GetUnsigned(l, 1, "rshift"),
                -LibUtils.GetSigned(l, 2, "rshift")
                );
        }

        public static int lshift(LuaState l)
        {
            return b_shift(l,
                LibUtils.GetUnsigned(l, 1, "lshift"),
                LibUtils.GetSigned(l, 2, "lshift")
            );
        }

        public static int bor(LuaState l)
        {
            int i, n = l.GetTop();
            uint r = 0;
            for (i = 1; i <= n; i++)
                r |= LibUtils.GetUnsigned(l, i, "bor");
            l.Push(new LuaValue(r));
            return 1;
        }
        
        public static int bxor(LuaState l)
        {
            int i, n = l.GetTop();
            uint r = 0;
            for (i = 1; i <= n; i++)
                r ^= LibUtils.GetUnsigned(l, i, "bxor");
            l.Push(new LuaValue(r));
            return 1;
        }

        static int fieldargs(LuaState l, int farg, string func, out int width)
        {
            width = 0;
            int f = LibUtils.GetSigned(l, farg, func);
            int w = 1;
            if (farg + 1 <= l.GetTop())
            {
                if (l.Value(farg + 1).TryGetNumber(out var n))
                    w = (int) n;
                else
                    w = 1;
            }
            LibUtils.ArgCheck(0 <= f, farg, func, "field cannot be negative");
            LibUtils.ArgCheck(0 < w, farg + 1, func, "width must be positive");
            if (f + w > NBITS)
                throw new Exception("trying to access non-existent bits");
            width = w;
            return f;
        }

        private const uint ALLONES = uint.MaxValue;
        static uint mask(int n) => (~((ALLONES << 1) << ((n) - 1))); 
        
        public static int extract(LuaState l)
        {
            int w;
            uint r = LibUtils.GetUnsigned(l, 1, "extract");
            int f = fieldargs(l, 2, "extract", out w);
            r = (r >> f) & mask(w);
            l.Push(new LuaValue(r));
            return 1;
        }

        public static int replace(LuaState l)
        {
            int w;
            uint r = LibUtils.GetUnsigned(l, 1, "replace");
            uint v = LibUtils.GetUnsigned(l, 2, "replace");
            int f = fieldargs(l, 3, "replace", out w);
            uint m = mask(w);
            v &= m;
            r = (r & ~(m << f)) | (v << f);
            l.Push(new LuaValue(r));
            return 1;
        }
        
        private static (string, DelegateClrFunction)[] funcs =
        {
            ("arshift", new DelegateClrFunction(arshift)),
            ("band", new DelegateClrFunction(band)),
            ("bnot", new DelegateClrFunction(bnot)),
            ("bor", new DelegateClrFunction(bor)),
            ("bxor", new DelegateClrFunction(bxor)),
            ("btest", new DelegateClrFunction(btest)),
            ("extract", new DelegateClrFunction(extract)),
            ("lrotate", new DelegateClrFunction(lrotate)),
            ("lshift", new DelegateClrFunction(lshift)),
            ("replace", new DelegateClrFunction(replace)),
            ("rrotate", new DelegateClrFunction(rrotate)),
            ("rshift", new DelegateClrFunction(rshift)),
        };
        
        public static void Register(LuaState state)
        {
            LibUtils.CreateLib(state, "bit32", funcs);
        }
        
    }
}