using System;
using System.Threading;
using PasifeLua.Interop;

namespace PasifeLua.Libs
{
    public static class MathLib
    {
        public static int abs(LuaState s)
        {
            s.Push(Math.Abs(LibUtils.GetNumber(s, 1, "abs")));
            return 1;
        }

        public static int acos(LuaState s)
        {
            s.Push(Math.Acos(LibUtils.GetNumber(s, 1, "acos")));
            return 1;
        }
        
        public static int asin(LuaState s)
        {
            s.Push(Math.Asin(LibUtils.GetNumber(s, 1, "asin")));
            return 1;
        }
        
        public static int atan(LuaState s)
        {
            s.Push(Math.Atan(LibUtils.GetNumber(s, 1, "atan")));
            return 1;
        }
        
        public static int atan2(LuaState s)
        {
            s.Push(Math.Atan2(
                LibUtils.GetNumber(s, 1, "atan2"),
                LibUtils.GetNumber(s, 2, "atan2")
            ));
            return 1;
        }
        
        public static int ceil(LuaState s)
        {
            s.Push(Math.Ceiling(LibUtils.GetNumber(s, 1, "ceil")));
            return 1;
        }
        public static int cos(LuaState s)
        {
            s.Push(Math.Cos(LibUtils.GetNumber(s, 1, "cos")));
            return 1;
        }
        
        public static int cosh(LuaState s)
        {
            s.Push(Math.Cosh(LibUtils.GetNumber(s, 1, "cosh")));
            return 1;
        }

        public static int deg(LuaState s)
        {
            s.Push( LibUtils.GetNumber(s, 1, "exp") * (180.0 / Math.PI));
            return 1;
        }
        
        public static int exp(LuaState s)
        {
            s.Push(Math.Exp(LibUtils.GetNumber(s, 1, "exp")));
            return 1;
        }
        
        public static int floor(LuaState s)
        {
            s.Push(Math.Floor(LibUtils.GetNumber(s, 1, "floor")));
            return 1;
        }
        
        public static int fmod(LuaState s)
        {
            var x =  LibUtils.GetNumber(s, 1, "fmod");
            var y =  LibUtils.GetNumber(s, 2, "fmod");
            s.Push(Math.IEEERemainder(x, y));
            return 1;
        }

        // http://stackoverflow.com/questions/389993/extracting-mantissa-and-exponent-from-double-in-c-sharp
        public static int frexp(LuaState s)
        {
            double d =  LibUtils.GetNumber(s, 1, "frexp");
            
            // Translate the double into sign, exponent and mantissa.
            long bits = BitConverter.DoubleToInt64Bits(d);
            // Note that the shift is sign-extended, hence the test against -1 not 1
            bool negative = (bits < 0);
            int exponent = (int) ((bits >> 52) & 0x7ffL);
            long mantissa = bits & 0xfffffffffffffL;

            // Subnormal numbers; exponent is effectively one higher,
            // but there's no extra normalisation bit in the mantissa
            if (exponent==0)
            {
                exponent++;
            }
            // Normal numbers; leave exponent as it is but add extra
            // bit to the front of the mantissa
            else
            {
                mantissa = mantissa | (1L<<52);
            }

            // Bias the exponent. It's actually biased by 1023, but we're
            // treating the mantissa as m.0 rather than 0.m, so we need
            // to subtract another 52 from it.
            exponent -= 1075;

            if (mantissa == 0)
            {
                s.Push(0);
                s.Push(0);
                return 2;
            }
            
            /* Normalize */
            while((mantissa & 1) == 0) 
            {    /*  i.e., Mantissa is even */
                mantissa >>= 1;
                exponent++;
            }

            double m = (double)mantissa;
            double e = (double)exponent;
            while( m >= 1 )
            {
                m /= 2.0;
                e += 1.0;
            }

            if( negative ) m = -m;
            s.Push(m);
            s.Push(e);
            return 2;
        }

        public static int ldexp(LuaState s)
        {
            double m = LibUtils.GetNumber(s, 1, "ldexp");
            double e =  LibUtils.GetNumber(s, 2, "ldexp");
            s.Push(m * Math.Pow(2, e));
            return 1;
        }

        public static int log(LuaState s)
        {
            double x =  LibUtils.GetNumber(s, 1, "log");
            if (s.GetTop() > 1) {
                double _b =  LibUtils.GetNumber(s, 2, "log");
                s.Push(Math.Log(x, _b));
            }
            else {
                s.Push(Math.Log(x));
            }
            return 1;
        }

        public static int max(LuaState s)
        {
            int n = s.GetTop();
            double x = LibUtils.GetNumber(s, 1, "max");
            for (int i = 2; i <= n; i++)
            {
                var y = LibUtils.GetNumber(s, i, "max");
                if (y > x) x = y;
            }
            s.Push(new LuaValue(x));
            return 1;
        }
        
        public static int min(LuaState s)
        {
            int n = s.GetTop();
            double x = LibUtils.GetNumber(s, 1, "max");
            for (int i = 2; i <= n; i++)
            {
                var y = LibUtils.GetNumber(s, i, "max");
                if (y < x) x = y;
            }
            s.Push(new LuaValue(x));
            return 1;
        }
        
        public static int modf(LuaState s)
        {
            var x =  LibUtils.GetNumber(s, 1, "modf");
            s.Push(Math.Floor(x));
            s.Push(x - Math.Floor(x));
            return 2;
        }
        
        public static int pow(LuaState s)
        {
            var x =  LibUtils.GetNumber(s, 1, "pow");
            var y =  LibUtils.GetNumber(s, 2, "pow");
            s.Push(Math.Pow(x, y));
            return 1;
        }

        public static int rad(LuaState s)
        {
            var x =  LibUtils.GetNumber(s, 1, "rad");
            s.Push(x * (Math.PI / 180.0));
            return 1;
        }
        
        static ThreadLocal<Random> threadrand = new ThreadLocal<Random>();

        static int random(LuaState s)
        {
            Random rand;
            if (!threadrand.IsValueCreated)
                threadrand.Value = rand = new Random();
            else
                rand = threadrand.Value;
            if (s.GetTop() == 2) {
                var m =  LibUtils.GetNumber(s, 1, "random");
                var n =  LibUtils.GetNumber(s, 2, "random");
                if (m < n) {
                    s.Push(rand.Next((int) m, (int) n + 1));
                }
                else {
                    s.Push(rand.Next((int) n, (int) m + 1));
                }
            }
            else if (s.GetTop() == 1)
            {
                var m =  LibUtils.GetNumber(s, 1, "random");
                if (m < 1)
                    s.Push(rand.Next((int) m, 1));
                else
                    s.Push(rand.Next(1, (int) m));
            }
            else {
                s.Push(rand.NextDouble());
            }
            return 1;
        }

        static int randomseed(LuaState s)
        {
            var x =  LibUtils.GetNumber(s, 1, "randomseed");
            threadrand.Value = new Random(x.GetHashCode());
            return 0;
        }
        
        public static int sin(LuaState s)
        {
            s.Push(Math.Sin(LibUtils.GetNumber(s, 1, "sin")));
            return 1;
        }
        
        public static int sinh(LuaState s)
        {
            s.Push(Math.Sinh(LibUtils.GetNumber(s, 1, "sinh")));
            return 1;
        }
        
        public static int sqrt(LuaState s)
        {
            s.Push(Math.Sqrt(LibUtils.GetNumber(s, 1, "sqrt")));
            return 1;
        }
        
        public static int tan(LuaState s)
        {
            s.Push(Math.Tan(LibUtils.GetNumber(s, 1, "tan")));
            return 1;
        }
        
        public static int tanh(LuaState s)
        {
            s.Push(Math.Tanh(LibUtils.GetNumber(s, 1, "tanh")));
            return 1;
        }
        
        private static (string, DelegateClrFunction)[] funcs =
        {
            ("abs", new DelegateClrFunction(abs)),
            ("acos", new DelegateClrFunction(acos)),
            ("asin", new DelegateClrFunction(asin)),
            ("atan", new DelegateClrFunction(atan)),
            ("atan2", new DelegateClrFunction(atan2)),
            ("ceil", new DelegateClrFunction(ceil)),
            ("cos", new DelegateClrFunction(cos)),
            ("cosh", new DelegateClrFunction(cosh)),
            ("deg", new DelegateClrFunction(deg)),
            ("exp", new DelegateClrFunction(exp)),
            ("floor", new DelegateClrFunction(floor)),
            ("fmod", new DelegateClrFunction(fmod)),
            ("frexp", new DelegateClrFunction(frexp)),
            ("ldexp", new DelegateClrFunction(ldexp)),
            ("log", new DelegateClrFunction(log)),
            ("max", new DelegateClrFunction(max)),
            ("min", new DelegateClrFunction(min)),
            ("modf", new DelegateClrFunction(modf)),
            ("pow", new DelegateClrFunction(pow)),
            ("rad", new DelegateClrFunction(rad)),
            ("random", new DelegateClrFunction(random)),
            ("randomseed", new DelegateClrFunction(randomseed)),
            ("sin", new DelegateClrFunction(sin)),
            ("sinh", new DelegateClrFunction(sinh)),
            ("sqrt", new DelegateClrFunction(sqrt)),
            ("tan", new DelegateClrFunction(tan)),
            ("tanh", new DelegateClrFunction(tanh))
        };

        public static void Register(LuaState state)
        {
            var math = LibUtils.CreateLib(state, "math", funcs);
            //constants
            math["huge"] = new LuaValue(double.PositiveInfinity);
            math["pi"] = new LuaValue(Math.PI);
        }
    }
}