//lexical analyser ported from Lua 5.2.4

using System;
using System.IO;
using System.Text;
using System.Threading;
//using statics
using static PasifeLua.Constants;
using static PasifeLua.luac.RESERVED;
using static PasifeLua.Utils;

namespace PasifeLua.luac
{
    enum RESERVED
    {
        /* terminal symbols denoted by reserved words */
        TK_AND = FIRST_RESERVED, TK_BREAK,
        TK_DO, TK_ELSE, TK_ELSEIF, TK_END, TK_FALSE, TK_FOR, TK_FUNCTION,
        TK_GOTO, TK_IF, TK_IN, TK_LOCAL, TK_NIL, TK_NOT, TK_OR, TK_REPEAT,
        TK_RETURN, TK_THEN, TK_TRUE, TK_UNTIL, TK_WHILE,
        /* other terminal symbols */
        TK_CONCAT, TK_DOTS, TK_EQ, TK_GE, TK_LE, TK_NE, TK_DBCOLON, TK_EOS,
        TK_NUMBER, TK_NAME, TK_STRING
    }
    
    struct SemInfo
    {
        public double r;
        public string ts;
    }

    struct Token
    {
        public int token;
        public SemInfo seminfo;
    }
    
    class Lexer
    {
        //llex.h
        public int current;
        public int linenumber;
        public int lastline;
        public Token t;
        public Token lookahead;
        public FuncState fs;
        public StringBuilder buffer;
        public TextReader z;
        public Dyndata dyd;
        public string source;
        public string envn;
        public char decpoint;

        public int _CallLevel;
        //public functions

        public void luaX_setinput(TextReader z, string source, int firstchar)
        {
            decpoint = '.';
            current = firstchar;
            lookahead.token = (int)TK_EOS;
            this.z = z;
            linenumber = 1;
            lastline = 1;
            this.source = source;
            this.envn = LUA_ENV;
        }

        public void luaX_next()
        {
            lastline = linenumber;
            if (lookahead.token != (int)TK_EOS) {
                t = lookahead;
                lookahead.token = (int) TK_EOS;
            }
            else {
                t.token = llex(ref t.seminfo);
            }

        }

        public int luaX_lookahead()
        {
            LAssert(lookahead.token == (int) TK_EOS);
            lookahead.token = llex(ref lookahead.seminfo);
            return lookahead.token;
        }
        
        /* luaX_newstring skipped, not relevant */

        public void luaX_syntaxerror(string msg)
        {
            lexerror(msg, t.token);
        }
        public string luaX_token2str(int token)
        {
            if (token < FIRST_RESERVED)
            {
                LAssert(token == (byte) token);
                return lisprint(token) ? ((char) token).ToString() : $"char({token})";
            }
            else
            {
                return luaX_tokens[token - FIRST_RESERVED];
            }
        }

        //llex.c
        int next() => (current = z.Read());
        
        bool currIsNewLine() => (current == (int)'\n' || current == (int)'\r');
            
        /* ORDER RESERVED */
        private static readonly string[] luaX_tokens = {
            "and", "break", "do", "else", "elseif",
            "end", "false", "for", "function", "goto", "if",
            "in", "local", "nil", "not", "or", "repeat",
            "return", "then", "true", "until", "while",
            "..", "...", "==", ">=", "<=", "~=", "::", "<eof>",
            "<number>", "<name>", "<string>"
        };

        void save_and_next() { save(current); next(); }
        void save(int c)
        {
            buffer.Append((char)c);
        }
        
        string txtToken(int token)
        {
            switch (token)
            {
                case (int)TK_NAME:
                case (int)TK_NUMBER:
                case (int)TK_STRING:
                    return buffer.ToString();
                default:
                    return luaX_token2str(token);
            }
        }

        void lexerror(string msg, int token) {
            string m = $"{source}:{linenumber}: {msg}";
            if (token > 0)
            {
                throw new LuaCompilerErrorException($"{m} near {txtToken(token)}");
            }
            throw new LuaCompilerErrorException(m);
        }

        void inclinenumber()
        {
            int old = current;
            LAssert(currIsNewLine());
            next(); //skip '\n' or '\r'
            if (currIsNewLine() && current != old)
                next();
            if (++linenumber >= (int.MaxValue - 1))
                lexerror("chunk has too many lines", 0);
        }
        
        /*
        ** =======================================================
        ** LEXICAL ANALYZER
        ** =======================================================
        */

        bool check_next(string set)
        {
            if (current <= 0 || set.IndexOf((char) current) == -1)
                return false;
            save_and_next();
            return true;
        }

        void buffreplace(char from, char to)
        {
            buffer.Replace(from, to);
        }

        void resetbuffer()
        {
            if(buffer.Length > 0) buffer = new StringBuilder();
        }

        char getlocaledecpoint() => Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        bool buff2d(out double e) => luaO_str2d(buffer.ToString(), out e);

        /*
        ** in case of format error, try to change decimal point separator to
        ** the one defined in the current locale and check again
        */
        void trydecpoint(ref SemInfo seminfo)
        {
            var old = decpoint;
            decpoint = getlocaledecpoint();
            buffreplace(old, decpoint);
            if (!buff2d(out seminfo.r))
            {
                buffreplace(decpoint, '.');
                lexerror("malformed number", (int)TK_NUMBER);
            }
        }

        /* LUA_NUMBER */
        /*
        ** this function is quite liberal in what it accepts, as 'luaO_str2d'
        ** will reject ill-formed numerals.
        */
        void read_numeral(ref SemInfo seminfo)
        {
            string expo = "Ee";
            int first = current;
            LAssert(char.IsDigit((char)current));
            save_and_next();
            if (first == (int) '0' && check_next("Xx"))
                expo = "Pp";
            for (;;)
            {
                if (check_next(expo)) /* exponent part? */
                    check_next("+-"); /* optional exponent sign */
                if (lisxdigit(current) || current == '.')
                    save_and_next();
                else break;
            }
            buffreplace('.', decpoint);
            if (!buff2d(out seminfo.r))
                trydecpoint(ref seminfo);
        }
        
        /*
        ** skip a sequence '[=*[' or ']=*]' and return its number of '='s or
        ** -1 if sequence is malformed
        */
        int skip_sep()
        {
            int count = 0;
            int s = current;
            LAssert(s ==  '[' || s ==  ']');
            save_and_next();
            while (current == '=')
            {
                save_and_next();
                count++;
            }
            return current == s ? count : (-count) - 1;
        }

        void read_long_string(ref SemInfo seminfo, bool store, int sep)
        {
            save_and_next(); //skip 2nd [
            if(currIsNewLine()) //string starts with a new line?
                inclinenumber(); //skip it
            for (;;) {
                switch (current)
                {
                    case EOZ:
                        lexerror(store ? "unfinished long string" : "unfinished long comment", (int) TK_EOS);
                        break;
                    case ']': {
                        if (skip_sep() == sep)
                        {
                            save_and_next();
                            goto endloop;
                        }
                        break;
                    }
                    case '\n':
                    case '\r': {
                        save('\n');
                        inclinenumber();
                        if (!store) resetbuffer();
                        break;
                    }
                    default:
                    {
                        if (store) save_and_next();
                        else next();
                        break;
                    }
                }
            } endloop:
            if (store) {
                seminfo.ts = buffer.ToString(2 + sep, buffer.Length - 2 * (2 + sep));
            }
        }

        void escerror(Span<int> c, int n, string msg) {
            resetbuffer();
            save('\\');
            for(int i = 0; i < n && c[i] != EOZ; i++)
                save(c[i]);
            lexerror(msg, (int)TK_STRING);
        }

        int readhexaesc()
        {
            Span<int> c = stackalloc int[3];
            int i, r = 0;
            c[0] = 'x';
            for (i = 1; i < 3; i++) {
                c[i] = next();
                if (!lisxdigit(c[i]))
                    escerror(c, i + 1, "hexadecimal digit expected");
                r = (r << 4) + luaO_hexavalue(c[i]);
            }
            return r;
        }

        int readdecesc()
        {
            Span<int> c = stackalloc int[3];
            int i, r = 0;
            for (i = 0; i < 3 && char.IsDigit((char) current); i++) {
                c[i] = current;
                r = 10 * r + c[i] - '0';
                next();
            }
            if (r > byte.MaxValue)
                escerror(c, i, "decimal escape too large");
            return r;
        }

        void read_string(int del, ref SemInfo seminfo)
        {
            save_and_next(); // keep delimiter (for error messages)
            while (current != del) {
                switch (current) {
                    case EOZ:
                        lexerror("unfinished string", (int)TK_EOS);
                        break;
                    case '\n':
                    case '\r':
                        lexerror("unfinished string", (int)TK_STRING);
                        break;
                    case '\\':
                    {
                        int c;
                        next();
                        switch (current) {
                            case 'a': c = '\a'; goto read_save;
                            case 'b': c = '\b'; goto read_save;
                            case 'f': c = '\f'; goto read_save;
                            case 'n': c = '\n'; goto read_save;
                            case 'r': c = '\r'; goto read_save;
                            case 't': c = '\t'; goto read_save;
                            case 'v': c = '\v'; goto read_save;
                            case 'x': c = readhexaesc(); goto read_save;
                            case '\n': case '\r':
                                inclinenumber(); c = '\n'; goto only_save;
                            case '\\': case '\"': case '\'':
                                c = current; goto read_save;
                            case EOZ: goto no_save;
                            case 'z': {
                                next();
                                while (char.IsWhiteSpace((char) current)) {
                                    if (currIsNewLine()) inclinenumber();
                                    else next();
                                }
                                goto no_save;
                            }
                            default: {
                                if (!char.IsDigit((char) current))
                                    escerror(new []{ current }, 1, "invalid escape sequence");
                                c = readdecesc();
                                goto only_save;
                            }
                        }
                        read_save: next();
                        only_save: save(c);
                        no_save: break;
                    }
                    default:
                        save_and_next();
                        break;
                }
            }
            save_and_next(); //skip delimiter
            seminfo.ts = buffer.ToString(1, buffer.Length - 2);
        }

        public static int isreserved(string s)
        {
            for (int i = 0; i < NUM_RESERVED; i++) {
                if (s == luaX_tokens[i])
                    return i + 1;
            }
            return -1;
        }
        
        int llex(ref SemInfo seminfo)
        {
            resetbuffer();
            for (;;) {
                switch (current)
                {
                    case '\n':
                    case '\r': {
                        inclinenumber();
                        break;
                    }
                    case ' ':
                    case '\f':
                    case '\t':
                    case '\v': {
                        next();
                        break;
                    }
                    case '-':
                    {
                        next();
                        if (current != '-') return '-';
                        //comment
                        next();
                        if (current == '[') { //long comment?
                            int sep = skip_sep();
                            resetbuffer(); // may dirty the buffer
                            if (sep >= 0)
                            {
                                var si = new SemInfo();
                                read_long_string(ref si, false, sep);
                                resetbuffer(); //may dirty the buffer
                                break;
                            }
                        }
                        //short comment
                        while (!currIsNewLine() && current != EOZ)
                            next();
                        break;
                    }
                    case '[': { //long string or simply '['
                        int sep = skip_sep();
                        if (sep >= 0) {
                            read_long_string(ref seminfo, true, sep);
                            return (int) TK_STRING;
                        }
                        else if (sep == -1) return '[';
                        else lexerror("invalid long string delimiter", (int)TK_STRING);
                        break;
                    }
                    case '=': {
                        next();
                        if (current != '=') return '=';
                        else { next(); return (int) TK_EQ; }
                    }
                    case '<': {
                        next();
                        if (current != '=') return '<';
                        else { next(); return (int) TK_LE; }
                    }
                    case '>': {
                        next();
                        if (current != '=') return '>';
                        else { next(); return (int) TK_GE; }
                    }
                    case '~': {
                        next();
                        if (current != '=') return '~';
                        else { next(); return (int) TK_NE; }
                    }
                    case ':': {
                        next();
                        if (current != ':') return ':';
                        else { next(); return (int) TK_DBCOLON; }
                    }
                    case '"':
                    case '\'': { //short literal strings
                        read_string(current, ref seminfo);
                        return (int)TK_STRING;
                    }
                    case '.': {
                        save_and_next();
                        if (check_next("."))
                        {
                            if (check_next("."))
                                return (int) TK_DOTS;
                            else
                                return (int) TK_CONCAT;
                        }
                        else if (!char.IsDigit((char) current)) return '.';
                        goto case '0'; //fallthrough
                    }
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                    {
                        read_numeral(ref seminfo);
                        return (int)TK_NUMBER;
                    }
                    case EOZ: {
                        return (int)TK_EOS;
                    }
                    default: {
                        if (char.IsLetter((char) current) || current == '_') //identifier or reserved word?
                        {
                            string ts;
                            do {
                                save_and_next();
                            } while (char.IsLetterOrDigit((char)current) || current == '_');
                            ts = buffer.ToString();
                            int extra = isreserved(ts);
                            seminfo.ts = ts;
                            if (extra != -1) //reserved word?
                                return extra - 1 + FIRST_RESERVED;
                            else {
                                return (int) TK_NAME;
                            }
                        } else {
                          if(current >= 255)
                              lexerror("unexpected unicode character", 0);
                          int c = current;
                          next();
                          return c;
                        }
                    }
                }
            }
        }
    }
}