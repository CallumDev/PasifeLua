using System;
using System.Diagnostics;
using System.Globalization;

namespace PasifeLua
{
    static class Utils
    {
        public static int NextPowerOfTwo(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException("n", "Must be positive.");
            return (int)Math.Pow(2, Math.Ceiling(Math.Log(n, 2)));
        }
        
        [DebuggerHidden]
        public static void LAssert(bool condition)
        {
            if (!condition) throw new Exception("internal assertion failed");
        }

        public static bool lisprint(int c)
        {
            return (c >= 32 && c <= 126);
        }
        
        public static bool luaO_str2d(string str, out double num)
        {
            //Validate characters
            num = 0.0;
            bool hex = false;
            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsWhiteSpace(str[i]) ||
                    char.IsDigit(str[i]) ||
                    (str[i] >= 'A' && str[i] <= 'F') ||
                    (str[i] >= 'a' && str[i] <= 'f') ||
                    str[i] == '-' ||
                    str[i] == 'p' ||
                    str[i] == 'P' ||
                    str[i] == '+' ||
                    str[i] == '.')
                    continue;
					
                if (str[i] == 'x' || str[i] == 'X')
                {
                    hex = true;
                    continue;
                }

                return false;
            }
			
            //hex float
            if (hex)
            {
                if (ParseHexFloat(str, out num))
                    return true;
            }
            else
            {
                if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out num))
                    return true;
            }
            return false;
        }

        static string ReadHexProgressive(string s, ref double d, out int digits)
        {
            digits = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (lisxdigit(c))
                {
                    int v = luaO_hexavalue(c);
                    d *= 16.0;
                    d += v;
                    ++digits;
                }
                else
                {
                    return s.Substring(i);
                }
            }

            return string.Empty;
        }
        static bool ParseHexFloat(string s, out double result)
        {
            bool negate = false;
            result = 0.0;
            s = s.Trim();
            if (s[0] == '+')
                s = s.Substring(1);
            if (s[0] == '-') {
                negate = true;
                s = s.Substring(1);
            }
            if ((s.Length < 3) || s[0] != '0' || char.ToUpperInvariant(s[1]) != 'X')
                return false;

            s = s.Substring(2);
            double value = 0.0;
            int dummy, exp = 0;

            s = ReadHexProgressive(s, ref value, out dummy);

            if (s.Length > 0 && s[0] == '.')
            {
                s = s.Substring(1);
                s = ReadHexProgressive(s, ref value, out exp);
            }
			
            exp *= -4;

            if (s.Length > 0 && char.ToUpper(s[0]) == 'P')
            {
                if (s.Length == 1)
                    return false;
                s = s.Substring(s[1] == '+' ? 2 : 1);
                int exp1 = int.Parse(s, CultureInfo.InvariantCulture);
                if (exp1 < 0) return false; //can't add negative exponent
                exp += exp1;
                s = "";
            }

            if (s.Length > 0) return false;
            result = value * Math.Pow(2, exp);
            if (negate) result = -result;
            return true;
        }

        public static bool lisxdigit(int c)
        {
            return "0123456789abcdefABCDEF".IndexOf((char) c) != -1;
        }

        public static int luaO_hexavalue(int c)
        {
            c &= ~0x20;
            if (c <= 0x39)
                return c - 0x30;
            return 10 + (c - 0x41);
        }

        public static T check_exp<T>(bool condition, T value)
        {
            LAssert(condition);
            return value;
        }
        
        /*
        ** converts an integer to a "floating point byte", represented as
        ** (eeeeexxx), where the real value is (1xxx) * 2^(eeeee - 1) if
        ** eeeee != 0 and (xxx) otherwise.
        */
        public static int luaO_int2fb (uint x) {
            int e = 0;  /* exponent */
            if (x < 8) return (int)x;
            while (x >= 0x10) {
                x = (x+1) >> 1;
                e++;
            }
            return ((e+1) << 3) | ((int)(x) - 8);
        }


        /* converts back */
        public static int luaO_fb2int (int x) {
            int e = (x >> 3) & 0x1f;
            if (e == 0) return x;
            else return ((x & 7) + 8) << (e - 1);
        }
        
    }
}