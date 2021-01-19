using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PasifeLua.Bytecode;
//using statics
using static PasifeLua.Constants;
using static PasifeLua.luac.RESERVED;
using static PasifeLua.luac.expkind;
using static PasifeLua.luac.BinOpr;
using static PasifeLua.luac.UnOpr;
using static PasifeLua.Utils;
using static PasifeLua.luac.lcode;

namespace PasifeLua.luac
{
    /*
    ** Expression descriptor
    */

    enum expkind {
        VVOID,	/* no value */
        VNIL,
        VTRUE,
        VFALSE,
        VK,		/* info = index of constant in `k' */
        VKNUM,	/* nval = numerical value */
        VNONRELOC,	/* info = result register */
        VLOCAL,	/* info = local register */
        VUPVAL,       /* info = index of upvalue in 'upvalues' */
        VINDEXED,	/* t = table register/upvalue; idx = index R/K */
        VJMP,		/* info = instruction pc */
        VRELOCABLE,	/* info = instruction pc */
        VCALL,	/* info = instruction pc */
        VVARARG	/* info = instruction pc */
    }

    struct expdesc
    {
        public expkind k;
        [StructLayout(LayoutKind.Explicit)]
        public struct expdesc_union
        {
            public struct expdesc_ind
            {
                public short idx; //index (R/K)
                public byte t; //table (register or upvalue)
                public byte vt; //whether t is register (VLOCAL) or upvalue (VUPVAL)
            }
            [FieldOffset(0)] public expdesc_ind ind;
            [FieldOffset(0)] public int info; //for generic use
            [FieldOffset(0)] public double nval; //for vknum
        }
        public expdesc_union u;
        public int t; //patch list of exit when true
        public int f; //patch list of exit when false
    }

    //description of active local variable
    struct Vardesc {
        public short idx;
    }
    
    //description of pending goto statements and label statements
    struct Labeldesc {
        public string name; //label identifier
        public int pc; //position in code
        public int line; //line where it appeared
        public byte nactvar; //local level where it appears in current block
    }
    //list of labels or gotos
    struct Labellist
    {
        public RefList<Labeldesc> arr;
    }

    class Dyndata
    {
        public List<Vardesc> actvar = new List<Vardesc>();
        public Labellist gt;
        public Labellist label;
    }

    class BlockCnt
    {
        public BlockCnt previous; //chain
        public short firstlabel; //index of first label in this block
        public short firstgoto; //index of first pending goto in this block
        public byte nactvar; //activate locals outside the block
        public byte upval; //true if some variable in the block is an upvalue
        public byte isloop; //true if block is a loop
    }
    
    class FuncState
    {
        public CompilerPrototype f;
        public Dictionary<LuaValue, int> h;
        public BlockCnt bl;
        public FuncState prev; /* enclosing function */
        public Lexer ls; /* lexical state */
        public int pc;  /* next position to code (equivalent to `ncode') */
        public int lasttarget;   /* 'label' of last 'jump label' */
        public int jpc;  /* list of pending jumps to `pc' */
        public int nk;  /* number of elements in `k' */
        public int np;  /* number of elements in `p' */
        public int firstlocal;  /* index of first local var (in Dyndata array) */
        public short nlocvars;  /* number of elements in 'f->locvars' */
        public byte nactvar;  /* number of active local variables */
        public byte nups;  /* number of upvalues */
        public byte freereg;  /* first free register */
    }
    
    static class Parser
    {
        static void semerror(Lexer ls, string msg)
        {
            ls.t.token = 0;
            ls.luaX_syntaxerror(msg);
        }
        
        static void error_expected(Lexer ls, int token)
        {
            ls.luaX_syntaxerror($"{ls.luaX_token2str(token)} expected");
        }

        static void errorlimit(FuncState fs, int limit, string what)
        {
            string msg;
            int line = fs.f.LineDefined;
            string where = (line == 0) ? "main function" : $"function at line {line}";
            msg = $"too many {what} (limit is {limit}) in {where}";
            fs.ls.luaX_syntaxerror(msg);
        }

        static void checklimit(FuncState fs, int v, int l, string what)
        {
            if (v > l) errorlimit(fs, l, what);
        }

        static bool testnext(Lexer ls, int c)
        {
            if (ls.t.token == c) {
                ls.luaX_next();
                return true;
            }
            return false;
        }

        static void check(Lexer ls, int c)
        {
            if(ls.t.token != c)
                error_expected(ls, c);
        }

        static void checknext(Lexer ls, int c)
        {
            check(ls, c);
            ls.luaX_next();
        }

        static void check_condition(Lexer ls, bool c, string msg) {
            if (!c) ls.luaX_syntaxerror(msg);
        }

        static void check_match(Lexer ls, int what, int who, int where)
        {
            if (!testnext(ls, what))
            {
                if (where == ls.linenumber)
                    error_expected(ls, what);
                else {
                    ls.luaX_syntaxerror(
                     $"{ls.luaX_token2str(what)} expected (to close ${ls.luaX_token2str(who)} at line {where})"
                    );
                }
            }
        }

        static string str_checkname(Lexer ls)
        {
            check(ls, (int)TK_NAME);
            var ts = ls.t.seminfo.ts;
            ls.luaX_next();
            return ts;
        }

        static void init_exp(ref expdesc e, expkind k, int i)
        {
            e.f = e.t = NO_JUMP;
            e.k = k;
            e.u.info = i;
        }

        static void codestring(Lexer ls, ref expdesc e, string s)
        {
            init_exp(ref e, VK, luaK_stringK(ls.fs, s));
        }

        static void checkname(Lexer ls, ref expdesc e)
        {
            codestring(ls, ref e, str_checkname(ls));
        }

        static int registerlocalvar(Lexer ls, string varname)
        {
            var fs = ls.fs;
            var f = fs.f;
            f.LocalInfos.Add(new LocalInfo() {
                Name = varname
            });
            return fs.nlocvars++;
        }

        static void new_localvar(Lexer ls, string name)
        {
            var fs = ls.fs;
            var dyd = ls.dyd;
            int reg = registerlocalvar(ls, name);
            checklimit(fs, dyd.actvar.Count + 1 - fs.firstlocal, MAXVARS, "local variables");
            dyd.actvar.Add(new Vardesc() {idx = (short) reg});
        }
        

        static ref LocalInfo getlocvar(FuncState fs, int i)
        {
            int idx = fs.ls.dyd.actvar[fs.firstlocal + i].idx;
            LAssert(idx < fs.nlocvars);
            return ref fs.f.LocalInfos[idx];
        }

        static void adjustlocalvars(Lexer ls, int nvars)
        {
            FuncState fs = ls.fs;
            fs.nactvar = (byte) (fs.nactvar + nvars);
            for (; nvars > 0; nvars--)
            {
                getlocvar(fs, fs.nactvar - nvars).StartPC = fs.pc;
            }
        }

        static void removevars(FuncState fs, int toLevel)
        {
            int remCount = fs.nactvar - toLevel;
            //
            while (fs.nactvar > toLevel) 
            {
                getlocvar(fs, --fs.nactvar).EndPC = fs.pc;
            }
            //remove from actvar list
            while (remCount > 0) {
                fs.ls.dyd.actvar.RemoveAt(fs.ls.dyd.actvar.Count - 1);
                remCount--;
            }
        }

        static int searchupvalue(FuncState fs, string name)
        {
            for(int i = 0; i < fs.f.Upvalues.Count;i++)
                if (fs.f.Upvalues[i].Name == name)
                    return i;
            return -1;
        }

        static int newupvalue(FuncState fs, string name, ref expdesc v)
        {
            checklimit(fs, fs.nups + 1, MAXUPVAL, "upvalues");
            fs.f.Upvalues.Add(new UpvalueDescriptor() {
                Stack = (v.k == VLOCAL) ? 1 : 0,
                Index = v.u.info,
                Name = name
            });
            return fs.nups++;
        }

        static int searchvar(FuncState fs, string n)
        {
            for (int i = fs.nactvar - 1; i >= 0; i--)
            {
                if (getlocvar(fs,i).Name == n)
                    return i;
            }
            return -1;
        }

        static void markupval(FuncState fs, int level)
        {
            BlockCnt bl = fs.bl;
            while (bl.nactvar > level) bl = bl.previous;
            bl.upval = 1;
        }

        static expkind singlevaraux(FuncState fs, string n, ref expdesc var, int _base)
        {
            if (fs == null)
                return VVOID;
            else
            {
                int v = searchvar(fs, n);
                if (v >= 0) { //found?
                    init_exp(ref var, VLOCAL, v); //variable is local
                    if (_base == 0)
                        markupval(fs, v); //local will be used as an upval
                    return VLOCAL;
                }
                else
                {
                    int idx = searchupvalue(fs, n);
                    if (idx < 0)
                    {
                        if (singlevaraux(fs.prev, n, ref var, 0) == VVOID) //try upper levels
                            return VVOID; //not found: is a global
                        //else was local or upval
                        idx = newupvalue(fs, n, ref var);
                    }
                    init_exp(ref var, VUPVAL, idx);
                    return VUPVAL;
                }
            }
        }

        static void singlevar(Lexer ls, ref expdesc var)
        {
            string varname = str_checkname(ls);
            FuncState fs = ls.fs;
            if (singlevaraux(fs, varname, ref var, 1) == VVOID) { //global name?
                expdesc key = new expdesc();
                singlevaraux(fs, ls.envn, ref var, 1); //get environment
                LAssert(var.k == VLOCAL || var.k == VUPVAL);
                codestring(ls, ref key, varname);
                luaK_indexed(fs, ref var, ref key);
            }
        }
        
        static void adjust_assign (Lexer ls, int nvars, int nexps, ref expdesc e) {
            FuncState fs = ls.fs;
            int extra = nvars - nexps;
            if (hasmultret(e.k)) {
                extra++;  /* includes call itself */
                if (extra < 0) extra = 0;
                luaK_setreturns(fs, ref e, extra);  /* last exp. provides the difference */
                if (extra > 1) luaK_reserveregs(fs, extra-1);
            }
            else {
                if (e.k != VVOID) luaK_exp2nextreg(fs, ref e);  /* close last expression */
                if (extra > 0) {
                    int reg = fs.freereg;
                    luaK_reserveregs(fs, extra);
                    luaK_nil(fs, reg, extra);
                }
            }
        }
     
        //stack overflow protection
        private const int MAX_CALLS = 200;
        static void enterlevel(Lexer ls)
        {
            ls._CallLevel++;
            checklimit(ls.fs, ls._CallLevel, MAX_CALLS, "C levels");
        }
        static void leavelevel(Lexer ls)
        {
            ls._CallLevel--;
        }
        
        static void closegoto (Lexer ls, int g, ref Labeldesc label) {
            int i;
            FuncState fs = ls.fs;
            ref Labellist gl = ref ls.dyd.gt;
            ref Labeldesc gt = ref gl.arr[g];
            LAssert(gt.name == label.name);
            if (gt.nactvar < label.nactvar) {
                var vname = getlocvar(fs, gt.nactvar).Name;
                semerror(ls, $"<goto {gt.name}> at line {gt.line} jumps into the scope of local {vname}");
            }
            luaK_patchlist(fs, gt.pc, label.pc);
            /* remove goto from pending list */
            gl.arr.RemoveAt(g);
        }
        
        
        /*
        ** try to close a goto with existing labels; this solves backward jumps
        */
        static bool findlabel (Lexer ls, int g) {
            int i;
            BlockCnt bl = ls.fs.bl;
            Dyndata dyd = ls.dyd;
            ref Labeldesc gt = ref dyd.gt.arr[g];
            /* check labels in current block for a match */
            for (i = bl.firstlabel; i < dyd.label.arr.Count; i++) {
                ref Labeldesc lb = ref dyd.label.arr[i];
                if (lb.name == gt.name) {  /* correct label? */
                    if (gt.nactvar > lb.nactvar &&
                        (bl.upval != 0 || dyd.label.arr.Count > bl.firstlabel))
                        luaK_patchclose(ls.fs, gt.pc, lb.nactvar);
                    closegoto(ls, g, ref lb);  /* close it */
                    return true;
                }
            }
            return false;  /* label not found; cannot close goto */
        }

        static int newlabelentry(Lexer ls, ref Labellist l, string name, int line, int pc)
        {
            l.arr.Add(new Labeldesc()
            {
                name = name,
                line = line,
                nactvar = ls.fs.nactvar,
                pc = pc
            });
            return l.arr.Count - 1;
        }
        
        /*
        ** check whether new label 'lb' matches any pending gotos in current
        ** block; solves forward jumps
        */
        static void findgotos (Lexer ls, ref Labeldesc lb) {
            ref Labellist gl = ref ls.dyd.gt;
            int i = ls.fs.bl.firstgoto;
            while (i < gl.arr.Count) {
                if (gl.arr[i].name == lb.name)
                    closegoto(ls, i, ref lb);
                else
                    i++;
            }
        }
        
        /*
        ** "export" pending gotos to outer level, to check them against
        ** outer labels; if the block being exited has upvalues, and
        ** the goto exits the scope of any variable (which can be the
        ** upvalue), close those variables being exited.
        */
        static void movegotosout (FuncState fs, BlockCnt bl) {
            int i = bl.firstgoto;
            ref Labellist gl = ref fs.ls.dyd.gt;
            /* correct pending gotos to current block and try to close it
               with visible labels */
            while (i < gl.arr.Count) {
                ref Labeldesc gt = ref gl.arr[i];
                if (gt.nactvar > bl.nactvar) {
                    if (bl.upval != 0)
                        luaK_patchclose(fs, gt.pc, bl.nactvar);
                    gt.nactvar = bl.nactvar;
                }
                if (!findlabel(fs.ls, i))
                    i++;  /* move to next one */
            }
        }
        
        static void enterblock (FuncState fs, out BlockCnt bl, bool isloop) {
            bl = new BlockCnt();
            bl.isloop = (byte) (isloop ? 1 : 0);
            bl.nactvar = fs.nactvar;
            bl.firstlabel = (short)fs.ls.dyd.label.arr.Count;
            bl.firstgoto = (short)fs.ls.dyd.gt.arr.Count;
            bl.upval = 0;
            bl.previous = fs.bl;
            fs.bl = bl;
            LAssert(fs.freereg == fs.nactvar);
        }

        static void breaklabel(Lexer ls)
        {
            int l = newlabelentry(ls, ref ls.dyd.label, "break", 0, ls.fs.pc);
            findgotos(ls, ref ls.dyd.label.arr[l]);
        }

        static void undefgoto(Lexer ls, ref Labeldesc gt)
        {
            string msg = Lexer.isreserved(gt.name) != -1
                ? $"<{gt.name} at line {gt.line}> not inside a loop"
                : $"no visible label '{gt.name}' for <goto> at line {gt.line}";
            semerror(ls, msg);
        }
        
        static void leaveblock (FuncState fs) {
            BlockCnt bl = fs.bl;
            Lexer ls = fs.ls;
            if (bl.previous != null && bl.upval != 0) {
                /* create a 'jump to here' to close upvalues */
                int j = luaK_jump(fs);
                luaK_patchclose(fs, j, bl.nactvar);
                luaK_patchtohere(fs, j);
            }
            if (bl.isloop != 0)
                breaklabel(ls);  /* close pending breaks */
            fs.bl = bl.previous;
            removevars(fs, bl.nactvar);
            LAssert(bl.nactvar == fs.nactvar);
            fs.freereg = fs.nactvar;  /* free registers */
            ls.dyd.label.arr.ShrinkTo(bl.firstlabel);  /* remove local labels */
            if (bl.previous != null)  /* inner block? */
                movegotosout(fs, bl);  /* update pending gotos to outer block */
            else if (bl.firstgoto < ls.dyd.gt.arr.Count)  /* pending gotos in outer block? */
                undefgoto(ls, ref ls.dyd.gt.arr[bl.firstgoto]);  /* error */
        }

        static CompilerPrototype addprototype(Lexer ls)
        {
            var proto = new CompilerPrototype();
            ls.fs.f.Protos.Add(proto);
            ls.fs.np++;
            return proto;
        }
        
        static void codeclosure (Lexer ls, ref expdesc v) {
            FuncState fs = ls.fs.prev;
            init_exp(ref v, VRELOCABLE, luaK_codeABx(fs, LuaOps.CLOSURE, 0, (uint)(fs.np - 1)));
            luaK_exp2nextreg(fs, ref v);  /* fix it at the last register */
        }
        
        static void open_func (Lexer ls, FuncState fs, out BlockCnt bl) {
            CompilerPrototype f;
            fs.prev = ls.fs;  /* linked list of funcstates */
            fs.ls = ls;
            ls.fs = fs;
            fs.pc = 0;
            fs.lasttarget = 0;
            fs.jpc = NO_JUMP;
            fs.freereg = 0;
            fs.nk = 0;
            fs.np = 0;
            fs.nups = 0;
            fs.nlocvars = 0;
            fs.nactvar = 0;
            fs.firstlocal = ls.dyd.actvar.Count;
            fs.bl = null;
            f = fs.f;
            f.Source = ls.source;
            f.MaxStackSize = 2;  /* registers 0/1 are always valid */
            fs.h = new Dictionary<LuaValue, int>();
            enterblock(fs, out bl, false);
        }

        static void close_func(Lexer ls)
        {
            var fs = ls.fs;
            var f = fs.f;
            luaK_ret(fs, 0, 0);
            leaveblock(fs);
            //finalise (?)
            ls.fs = fs.prev;
        }
        
        /*============================================================*/
        /* GRAMMAR RULES */
        /*============================================================*/


        /*
        ** check whether current token is in the follow set of a block.
        ** 'until' closes syntactical blocks, but do not close scope,
        ** so it handled in separate.
        */
        static bool block_follow (Lexer ls, bool withuntil) {
            switch ((RESERVED)ls.t.token) {
                case TK_ELSE: case TK_ELSEIF:
                case TK_END: case TK_EOS:
                    return true;
                case TK_UNTIL: return withuntil;
                default: return false;
            }
        }
        
        static void statlist (Lexer ls) {
            /* statlist -> { stat [`;'] } */
            while (!block_follow(ls, true)) {
                if (ls.t.token == (int)TK_RETURN) {
                    statement(ls);
                    return;  /* 'return' must be last statement */
                }
                statement(ls);
            }
        }
        
        static void fieldsel (Lexer ls, ref expdesc v) {
            /* fieldsel -> ['.' | ':'] NAME */
            FuncState fs = ls.fs;
            expdesc key = new expdesc();
            luaK_exp2anyregup(fs, ref v);
            ls.luaX_next();  /* skip the dot or colon */
            checkname(ls, ref key);
            luaK_indexed(fs, ref v, ref key);
        }

        static void yindex (Lexer ls, ref expdesc v) {
            /* index -> '[' expr ']' */
            ls.luaX_next();  /* skip the '[' */
            expr(ls, ref v);
            luaK_exp2val(ls.fs, ref v);
            checknext(ls, ']');
        }


        /*
        ** {======================================================================
        ** Rules for Constructors
        ** =======================================================================
        */
        
        struct ConsControl {
            public expdesc v;  /* last list item read */
              /* Pasife: table descriptor passed as arg CT*/
            public int nh;  /* total number of `record' elements */
            public int na;  /* total number of array elements */
            public int tostore;  /* number of array elements pending to be stored */
        };
        
        static void recfield (Lexer ls, ref ConsControl cc, ref expdesc ct) {
            /* recfield -> (NAME | `['exp1`]') = exp1 */
            FuncState fs = ls.fs;
            int reg = ls.fs.freereg;
            expdesc key = new expdesc(), val = new expdesc();
            int rkkey;
            if (ls.t.token == (int)TK_NAME) {
                checklimit(fs, cc.nh, int.MaxValue, "items in a constructor");
                checkname(ls, ref key);
            }
            else  /* ls.t.token == '[' */
                yindex(ls, ref key);
            cc.nh++;
            checknext(ls, '=');
            rkkey = luaK_exp2RK(fs, ref key);
            expr(ls, ref val);
            luaK_codeABC(fs, LuaOps.SETTABLE, ct.u.info, rkkey, luaK_exp2RK(fs, ref val));
            fs.freereg = (byte)reg;  /* free registers */
        }
        
        static void closelistfield (FuncState fs, ref ConsControl cc, ref expdesc ct) {
            if (cc.v.k == VVOID) return;  /* there is no list item */
            luaK_exp2nextreg(fs, ref cc.v);
            cc.v.k = VVOID;
            if (cc.tostore == LFIELDS_PER_FLUSH) {
                luaK_setlist(fs, ct.u.info, cc.na, cc.tostore);  /* flush */
                cc.tostore = 0;  /* no more items pending */
            }
        }
        
        static void lastlistfield (FuncState fs, ref ConsControl cc, ref expdesc ct) {
            if (cc.tostore == 0) return;
            if (hasmultret(cc.v.k)) {
                luaK_setmultret(fs, ref cc.v);
                luaK_setlist(fs, ct.u.info, cc.na, LUA_MULTRET);
                cc.na--;  /* do not count last expression (unknown number of elements) */
            }
            else {
                if (cc.v.k != VVOID)
                    luaK_exp2nextreg(fs, ref cc.v);
                luaK_setlist(fs, ct.u.info, cc.na, cc.tostore);
            }
        }
        
        static void listfield (Lexer ls, ref ConsControl cc) {
            /* listfield -> exp */
            expr(ls, ref cc.v);
            checklimit(ls.fs, cc.na, int.MaxValue, "items in a constructor");
            cc.na++;
            cc.tostore++;
        }
        
        static void field (Lexer ls, ref ConsControl cc, ref expdesc ct) {
            /* field -> listfield | recfield */
            switch(ls.t.token) {
                case (int)TK_NAME: {  /* may be 'listfield' or 'recfield' */
                    if (ls.luaX_lookahead() != '=')  /* expression? */
                        listfield(ls, ref cc);
                    else
                        recfield(ls, ref cc, ref ct);
                    break;
                }
                case '[': {
                    recfield(ls, ref cc, ref ct);
                    break;
                }
                default: {
                    listfield(ls, ref cc);
                    break;
                }
            }
        }
        
        static void constructor (Lexer ls, ref expdesc t) {
            /* constructor -> '{' [ field { sep field } [sep] ] '}'
               sep -> ',' | ';' */
            FuncState fs = ls.fs;
            int line = ls.linenumber;
            int pc = luaK_codeABC(fs, LuaOps.NEWTABLE, 0, 0, 0);
            ConsControl cc = new ConsControl();
            cc.na = cc.nh = cc.tostore = 0;
            init_exp(ref t, VRELOCABLE, pc);
            init_exp(ref cc.v, VVOID, 0);  /* no value (yet) */
            luaK_exp2nextreg(ls.fs, ref t);  /* fix it at stack top */
            checknext(ls, '{');
            do {
                LAssert(cc.v.k == VVOID || cc.tostore > 0);
                if (ls.t.token == '}') break;
                closelistfield(fs, ref cc, ref t);
                field(ls, ref cc, ref t);
            } while (testnext(ls, ',') || testnext(ls, ';'));
            check_match(ls, '}', '{', line);
            lastlistfield(fs, ref cc, ref t);
            fs.f.Code[pc].B = luaO_int2fb((uint)cc.na); /* set initial array size */
            fs.f.Code[pc].C = luaO_int2fb((uint)cc.nh); /* set initial table size */
        }

        /* }====================================================================== */

        static void parlist (Lexer ls) {
            /* parlist -> [ param { `,' param } ] */
            FuncState fs = ls.fs;
            CompilerPrototype f = fs.f;
            int nparams = 0;
            f.IsVararg = false;
            if (ls.t.token != ')') {  /* is `parlist' not empty? */
                do {
                    switch ((RESERVED)ls.t.token) {
                        case TK_NAME: {  /* param -> NAME */
                            new_localvar(ls, str_checkname(ls));
                            nparams++;
                            break;
                        }
                        case TK_DOTS: {  /* param -> `...' */
                            ls.luaX_next();
                            f.IsVararg = true;
                            break;
                        }
                        default: ls.luaX_syntaxerror("<name> or '...' expected");
                            break;
                    }
                } while (!f.IsVararg && testnext(ls, ','));
            }
            adjustlocalvars(ls, nparams);
            f.NumParams = (byte)(fs.nactvar);
            luaK_reserveregs(fs, fs.nactvar);  /* reserve register for parameters */
        }
        
        static void body (Lexer ls, ref expdesc e, bool ismethod, int line) {
            /* body ->  `(' parlist `)' block END */
            FuncState new_fs = new FuncState();
            BlockCnt bl;
            new_fs.f = addprototype(ls);
            new_fs.f.LineDefined = line;
            open_func(ls, new_fs, out bl);
            checknext(ls, '(');
            if (ismethod) {
                new_localvar(ls, "self");  /* create 'self' parameter */
                adjustlocalvars(ls, 1);
            }
            parlist(ls);
            checknext(ls, ')');
            statlist(ls);
            new_fs.f.LastLineDefined = ls.linenumber;
            check_match(ls, (int)TK_END, (int)TK_FUNCTION, line);
            codeclosure(ls, ref e);
            close_func(ls);
        }
        
                
        static int explist (Lexer ls, ref expdesc v) {
            /* explist -> expr { `,' expr } */
            int n = 1;  /* at least one expression */
            expr(ls, ref v);
            while (testnext(ls, ',')) {
                luaK_exp2nextreg(ls.fs, ref v);
                expr(ls, ref v);
                n++;
            }
            return n;
        }

        
        static void funcargs (Lexer ls, ref expdesc f, int line) {
            FuncState fs = ls.fs;
            expdesc args = new expdesc();
            int _base, nparams;
            switch (ls.t.token) {
                case '(': {  /* funcargs . `(' [ explist ] `)' */
                    ls.luaX_next();
                    if (ls.t.token == ')')  /* arg list is empty? */
                        args.k = VVOID;
                    else {
                        explist(ls, ref args);
                        luaK_setmultret(fs, ref args);
                    }
                    check_match(ls, ')', '(', line);
                    break;
                }
                case '{': {  /* funcargs . constructor */
                    constructor(ls, ref args);
                    break;
                }
                case (int)TK_STRING: {  /* funcargs . STRING */
                    codestring(ls, ref args, ls.t.seminfo.ts);
                    ls.luaX_next();  /* must use `seminfo' before `next' */
                    break;
                }
                default: {
                    ls.luaX_syntaxerror("function arguments expected");
                    return;
                }
            }
            LAssert(f.k == VNONRELOC);
            _base = f.u.info;  /* base register for call */
            if (hasmultret(args.k))
                nparams = LUA_MULTRET;  /* open call */
            else {
                if (args.k != VVOID)
                    luaK_exp2nextreg(fs, ref args);  /* close last argument */
                nparams = fs.freereg - (_base+1);
            }
            init_exp(ref f, VCALL, luaK_codeABC(fs, LuaOps.CALL,  _base, nparams+1, 2));
            luaK_fixline(fs, line);
            fs.freereg = (byte)(_base+1);  /* call remove function and arguments and leaves
                            (unless changed) one result */
        }
        /*
        ** {======================================================================
        ** Expression parsing
        ** =======================================================================
        */
        static void primaryexp (Lexer ls, ref expdesc v) {
            /* primaryexp -> NAME | '(' expr ')' */
            switch (ls.t.token) {
                case '(': {
                    int line = ls.linenumber;
                    ls.luaX_next();
                    expr(ls, ref v);
                    check_match(ls, ')', '(', line);
                    luaK_dischargevars(ls.fs, ref v);
                    return;
                }
                case (int)TK_NAME: {
                    singlevar(ls, ref v);
                    return;
                }
                default: {
                    ls.luaX_syntaxerror("unexpected symbol");
                    break;
                } 
            }
        }
        static void suffixedexp (Lexer ls, ref expdesc v) {
            /* suffixedexp ->
                 primaryexp { '.' NAME | '[' exp ']' | ':' NAME funcargs | funcargs } */
            FuncState fs = ls.fs;
            int line = ls.linenumber;
            primaryexp(ls, ref v);
            for (;;) {
                switch (ls.t.token) {
                    case '.': {  /* fieldsel */
                        fieldsel(ls, ref v);
                        break;
                    }
                    case '[': {  /* `[' exp1 `]' */
                        expdesc key = new expdesc();
                        luaK_exp2anyregup(fs, ref v);
                        yindex(ls, ref key);
                        luaK_indexed(fs, ref v, ref key);
                        break;
                    }
                    case ':': {  /* `:' NAME funcargs */
                        expdesc key = new expdesc();
                        ls.luaX_next();
                        checkname(ls, ref key);
                        luaK_self(fs, ref v, ref key);
                        funcargs(ls, ref v, line);
                        break;
                    }
                    case '(': case (int)TK_STRING: case '{': {  /* funcargs */
                        luaK_exp2nextreg(fs, ref v);
                        funcargs(ls, ref v, line);
                        break;
                    }
                    default: return;
                }
            }
        }
        
        static void simpleexp (Lexer ls, ref expdesc v) {
            /* simpleexp -> NUMBER | STRING | NIL | TRUE | FALSE | ... |
                            constructor | FUNCTION body | suffixedexp */
            switch ((RESERVED)ls.t.token) {
                case TK_NUMBER: {
                    init_exp(ref v, VKNUM, 0);
                    v.u.nval = ls.t.seminfo.r;
                    break;
                }
                case TK_STRING: {
                    codestring(ls, ref v, ls.t.seminfo.ts);
                    break;
                }
                case TK_NIL: {
                    init_exp(ref v, VNIL, 0);
                    break;
                }
                case TK_TRUE: {
                    init_exp(ref v, VTRUE, 0);
                    break;
                }
                case TK_FALSE: {
                    init_exp(ref v, VFALSE, 0);
                    break;
                }
                case TK_DOTS: {  /* vararg */
                    FuncState fs = ls.fs;
                    check_condition(ls, fs.f.IsVararg,
                        "cannot use \"...\" outside a vararg function");
                    init_exp(ref v, VVARARG, luaK_codeABC(fs, LuaOps.VARARG, 0, 1, 0));
                    break;
                }
                case (RESERVED)'{': {  /* constructor */
                    constructor(ls, ref v);
                    return;
                }
                case TK_FUNCTION: {
                    ls.luaX_next();
                    body(ls, ref v, false, ls.linenumber);
                    return;
                }
                default: {
                    suffixedexp(ls, ref v);
                    return;
                }
            }
            ls.luaX_next();
        }
        
        static UnOpr getunopr (int op) {
            switch (op) {
                case (int)TK_NOT: return OPR_NOT;
                case '-': return OPR_MINUS;
                case '#': return OPR_LEN;
                default: return OPR_NOUNOPR;
            }
        }


        static BinOpr getbinopr (int op) {
            switch (op) {
                case '+': return OPR_ADD;
                case '-': return OPR_SUB;
                case '*': return OPR_MUL;
                case '/': return OPR_DIV;
                case '%': return OPR_MOD;
                case '^': return OPR_POW;
                case (int)TK_CONCAT: return OPR_CONCAT;
                case (int)TK_NE: return OPR_NE;
                case (int)TK_EQ: return OPR_EQ;
                case '<': return OPR_LT;
                case (int)TK_LE: return OPR_LE;
                case '>': return OPR_GT;
                case (int)TK_GE: return OPR_GE;
                case (int)TK_AND: return OPR_AND;
                case (int)TK_OR: return OPR_OR;
                default: return OPR_NOBINOPR;
            }
        }

        private static readonly (byte left, byte right)[] priority =
        {
            (6, 6), (6, 6), (7, 7), (7, 7), (7, 7),  /* `+' `-' `*' `/' `%' */
            (10, 9), (5, 4),                 /* ^, .. (right associative) */
            (3, 3), (3, 3), (3, 3),          /* ==, <, <= */
            (3, 3), (3, 3), (3, 3),          /* ~=, >, >= */
            (2, 2), (1, 1)                   /* and, or */
        };
        private const int UNARY_PRIORITY = 8;
        
        /*
        ** subexpr -> (simpleexp | unop subexpr) { binop subexpr }
        ** where `binop' is any binary operator with a priority higher than `limit'
        */
        static BinOpr subexpr (Lexer ls, ref expdesc v, int limit) {
            BinOpr op;
            UnOpr uop;
            enterlevel(ls);
            uop = getunopr(ls.t.token);
            if (uop != OPR_NOUNOPR) {
                int line = ls.linenumber;
                ls.luaX_next();
                subexpr(ls, ref v, UNARY_PRIORITY);
                luaK_prefix(ls.fs, uop, ref v, line);
            }
            else simpleexp(ls, ref v);
            /* expand while operators have priorities higher than `limit' */
            op = getbinopr(ls.t.token);
            while (op != OPR_NOBINOPR && priority[(int)op].left > limit) {
                expdesc v2 = new expdesc();
                BinOpr nextop;
                int line = ls.linenumber;
                ls.luaX_next();
                luaK_infix(ls.fs, op, ref v);
                /* read sub-expression with higher priority */
                nextop = subexpr(ls, ref v2, priority[(int)op].right);
                luaK_posfix(ls.fs, op, ref v, ref v2, line);
                op = nextop;
            }
            leavelevel(ls);
            return op;  /* return first untreated operator */
        }

        static void expr(Lexer ls, ref expdesc v)
        {
            subexpr(ls, ref v, 0);
        }
        
        /*
        ** {======================================================================
        ** Rules for Statements
        ** =======================================================================
        */

        static void block (Lexer ls) {
            /* block -> statlist */
            FuncState fs = ls.fs;
            BlockCnt bl;
            enterblock(fs, out bl, false);
            statlist(ls);
            leaveblock(fs);
        }
        
        /*
        ** structure to chain all variables in the left-hand side of an
        ** assignment
        */
        class LHS_assign {
            public LHS_assign prev;
            public expdesc v;  /* variable (global, local, upvalue, or indexed) */
        };
        
        static void check_conflict (Lexer ls, LHS_assign lh, ref expdesc v) {
            FuncState fs = ls.fs;
            int extra = fs.freereg;  /* eventual position to save local variable */
            bool conflict = false;
            for (; lh != null; lh = lh.prev) {  /* check all previous assignments */
                if (lh.v.k == VINDEXED) {  /* assigning to a table? */
                    /* table is the upvalue/local being assigned now? */
                    if (lh.v.u.ind.vt == (byte)v.k && lh.v.u.ind.t == v.u.info)
                    {
                        conflict = true;
                        lh.v.u.ind.vt = (byte)VLOCAL;
                        lh.v.u.ind.t = (byte)extra;  /* previous assignment will use safe copy */
                    }
                    /* index is the local being assigned? (index cannot be upvalue) */
                    if (v.k == VLOCAL && lh.v.u.ind.idx == v.u.info) {
                        conflict = true;
                        lh.v.u.ind.idx = (byte)extra;  /* previous assignment will use safe copy */
                    }
                }
            }
            if (conflict) {
                /* copy upvalue/local value to a temporary (in position 'extra') */
                LuaOps op = (v.k == VLOCAL) ? LuaOps.MOVE : LuaOps.GETUPVAL;
                luaK_codeABC(fs, op, extra, v.u.info, 0);
                luaK_reserveregs(fs, 1);
            }
        }
        
        static void assignment (Lexer ls, LHS_assign lh, int nvars) {
            expdesc e = new expdesc();
            check_condition(ls, vkisvar((int)lh.v.k), "syntax error");
            if (testnext(ls, ',')) {  /* assignment . ',' suffixedexp assignment */
                var nv = new LHS_assign();
                nv.prev = lh;
                suffixedexp(ls, ref nv.v);
                if (nv.v.k != VINDEXED)
                    check_conflict(ls, lh, ref nv.v);
                checklimit(ls.fs, nvars + ls._CallLevel, MAX_CALLS,
                    "C levels");
                assignment(ls, nv, nvars+1);
            }
            else {  /* assignment . `=' explist */
                int nexps;
                checknext(ls, '=');
                nexps = explist(ls, ref e);
                if (nexps != nvars) {
                    adjust_assign(ls, nvars, nexps, ref e);
                    if (nexps > nvars)
                        ls.fs.freereg -= (byte)(nexps - nvars);  /* remove extra values */
                }
                else {
                    luaK_setoneret(ls.fs, ref e);  /* close last expression */
                    luaK_storevar(ls.fs, ref lh.v, ref e);
                    return;  /* avoid default */
                }
            }
            init_exp(ref e, VNONRELOC, ls.fs.freereg-1);  /* default assignment */
            luaK_storevar(ls.fs, ref lh.v, ref e);
        }
        
        static int cond (Lexer ls) {
            /* cond -> exp */
            expdesc v = new expdesc();
            expr(ls, ref v);  /* read condition */
            if (v.k == VNIL) v.k = VFALSE;  /* `falses' are all equal here */
            luaK_goiftrue(ls.fs, ref v);
            return v.f;
        }
        
        static void gotostat (Lexer ls, int pc) {
            int line = ls.linenumber;
            string label;
            int g;
            if (testnext(ls, (int)TK_GOTO))
                label = str_checkname(ls);
            else {
                ls.luaX_next();  /* skip break */
                label = "break";
            }
            g = newlabelentry(ls, ref ls.dyd.gt, label, line, pc);
            findlabel(ls, g);  /* close it if label already defined */
        }
        
        /* check for repeated labels on the same block */
        static void checkrepeated (FuncState fs, ref Labellist ll, string label) {
            int i;
            for (i = fs.bl.firstlabel; i < ll.arr.Count; i++) {
                if (label ==ll.arr[i].name) {
                    semerror(fs.ls, $"label '{label}' already defined on line {ll.arr[i].line}");
                }
            }
        }

        /* skip no-op statements */
        static void skipnoopstat (Lexer ls) {
            while (ls.t.token == ';' || ls.t.token == (int)TK_DBCOLON)
                statement(ls);
        }
        
        static void labelstat (Lexer ls, string label, int line) {
            /* label -> '::' NAME '::' */
            FuncState fs = ls.fs;
            ref Labellist ll = ref ls.dyd.label;
            int l;  /* index of new label being created */
            checkrepeated(fs, ref ll, label);  /* check for repeated labels */
            checknext(ls, (int)TK_DBCOLON);  /* skip double colon */
            /* create new entry for this label */
            l = newlabelentry(ls, ref ll, label, line, fs.pc);
            skipnoopstat(ls);  /* skip other no-op statements */
            if (block_follow(ls, false)) {  /* label is last no-op statement in the block? */
                /* assume that locals are already out of scope */
                ll.arr[l].nactvar = fs.bl.nactvar;
            }
            findgotos(ls, ref ll.arr[l]);
        }
        
        static void whilestat (Lexer ls, int line) {
            /* whilestat -> WHILE cond DO block END */
            FuncState fs = ls.fs;
            int whileinit;
            int condexit;
            BlockCnt bl;
            ls.luaX_next();  /* skip WHILE */
            whileinit = luaK_getlabel(fs);
            condexit = cond(ls);
            enterblock(fs, out bl, true);
            checknext(ls, (int)TK_DO);
            block(ls);
            luaK_jumpto(fs, whileinit);
            check_match(ls, (int)TK_END, (int)TK_WHILE, line);
            leaveblock(fs);
            luaK_patchtohere(fs, condexit);  /* false conditions finish the loop */
        }
        
        static void repeatstat (Lexer ls, int line) {
            /* repeatstat -> REPEAT block UNTIL cond */
            int condexit;
            FuncState fs = ls.fs;
            int repeat_init = luaK_getlabel(fs);
            BlockCnt bl1, bl2;
            enterblock(fs, out bl1, true);  /* loop block */
            enterblock(fs, out bl2, false);  /* scope block */
            ls.luaX_next();  /* skip REPEAT */
            statlist(ls);
            check_match(ls, (int)TK_UNTIL, (int)TK_REPEAT, line);
            condexit = cond(ls);  /* read condition (inside scope block) */
            if (bl2.upval != 0)  /* upvalues? */
                luaK_patchclose(fs, condexit, bl2.nactvar);
            leaveblock(fs);  /* finish scope */
            luaK_patchlist(fs, condexit, repeat_init);  /* close the loop */
            leaveblock(fs);  /* finish loop */
        }
        
        static int exp1 (Lexer ls) {
            expdesc e = new expdesc();
            int reg;
            expr(ls, ref e);
            luaK_exp2nextreg(ls.fs, ref e);
            LAssert(e.k == VNONRELOC);
            reg = e.u.info;
            return reg;
        }
        
        static void forbody (Lexer ls, int _base, int line, int nvars, int isnum) {
            /* forbody -> DO block */
            BlockCnt bl;
            FuncState fs = ls.fs;
            int prep, endfor;
            adjustlocalvars(ls, 3);  /* control variables */
            checknext(ls, (int)TK_DO);
            prep = isnum != 0 ? luaK_codeAsBx(fs, LuaOps.FORPREP, _base, NO_JUMP) : luaK_jump(fs);
            enterblock(fs, out bl, false);  /* scope for declared variables */
            adjustlocalvars(ls, nvars);
            luaK_reserveregs(fs, nvars);
            block(ls);
            leaveblock(fs);  /* end of scope for declared variables */
            luaK_patchtohere(fs, prep);
            if (isnum != 0)  /* numeric for? */
                endfor = luaK_codeAsBx(fs, LuaOps.FORLOOP, _base, NO_JUMP);
            else {  /* generic for */
                luaK_codeABC(fs, LuaOps.TFORCALL, _base, 0, nvars);
                luaK_fixline(fs, line);
                endfor = luaK_codeAsBx(fs, LuaOps.TFORLOOP, _base + 2, NO_JUMP);
            }
            luaK_patchlist(fs, endfor, prep + 1);
            luaK_fixline(fs, line);
        }
        
        static void fornum (Lexer ls, string varname, int line) {
            /* fornum -> NAME = exp1,exp1[,exp1] forbody */
            FuncState fs = ls.fs;
            int _base = fs.freereg;
            new_localvar(ls, "(for index)");
            new_localvar(ls, "(for limit)");
            new_localvar(ls, "(for step)");
            new_localvar(ls, varname);
            checknext(ls, '=');
            exp1(ls);  /* initial value */
            checknext(ls, ',');
            exp1(ls);  /* limit */
            if (testnext(ls, ','))
                exp1(ls);  /* optional step */
            else {  /* default step = 1 */
                luaK_codek(fs, fs.freereg, luaK_numberK(fs, 1));
                luaK_reserveregs(fs, 1);
            }
            forbody(ls, _base, line, 1, 1);
        }
        
        static void forlist (Lexer ls, string indexname) {
            /* forlist -> NAME {,NAME} IN explist forbody */
            FuncState fs = ls.fs;
            expdesc e = new expdesc();
            int nvars = 4;  /* gen, state, control, plus at least one declared var */
            int line;
            int _base = fs.freereg;
            /* create control variables */
            new_localvar(ls, "(for generator)");
            new_localvar(ls, "(for state)");
            new_localvar(ls, "(for control)");
            /* create declared variables */
            new_localvar(ls, indexname);
            while (testnext(ls, ',')) {
                new_localvar(ls, str_checkname(ls));
                nvars++;
            }
            checknext(ls, (int)TK_IN);
            line = ls.linenumber;
            adjust_assign(ls, 3, explist(ls, ref e), ref e);
            luaK_checkstack(fs, 3);  /* extra space to call generator */
            forbody(ls, _base, line, nvars - 3, 0);
        }
        
        static void forstat (Lexer ls, int line) {
            /* forstat -> FOR (fornum | forlist) END */
            FuncState fs = ls.fs;
            string varname;
            BlockCnt bl;
            enterblock(fs, out bl, true);  /* scope for loop and control variables */
            ls.luaX_next();  /* skip `for' */
            varname = str_checkname(ls);  /* first variable name */
            switch (ls.t.token) {
                case '=': fornum(ls, varname, line); break;
                case ',': case (int)TK_IN: forlist(ls, varname); break;
                default: ls.luaX_syntaxerror("'=' or 'in expected"); return;
            }
            check_match(ls, (int)TK_END, (int)TK_FOR, line);
            leaveblock(fs);  /* loop scope (`break' jumps to this point) */
        }
        static void test_then_block (Lexer ls, ref int escapelist) {
            /* test_then_block -> [IF | ELSEIF] cond THEN block */
            BlockCnt bl;
            FuncState fs = ls.fs;
            expdesc v = new expdesc();
            int jf;  /* instruction to skip 'then' code (if condition is false) */
            ls.luaX_next();  /* skip IF or ELSEIF */
            expr(ls, ref v);  /* read condition */
            checknext(ls, (int)TK_THEN);
            if (ls.t.token == (int)TK_GOTO || ls.t.token == (int)TK_BREAK) {
                luaK_goiffalse(ls.fs, ref v);  /* will jump to label if condition is true */
                enterblock(fs, out bl, false);  /* must enter block before 'goto' */
                gotostat(ls, v.t);  /* handle goto/break */
                skipnoopstat(ls);  /* skip other no-op statements */
                if (block_follow(ls, false)) {  /* 'goto' is the entire block? */
                    leaveblock(fs);
                    return;  /* and that is it */
                }
                else  /* must skip over 'then' part if condition is false */
                    jf = luaK_jump(fs);
            }
            else {  /* regular case (not goto/break) */
                luaK_goiftrue(ls.fs, ref v);  /* skip over block if condition is false */
                enterblock(fs, out bl, false);
                jf = v.f;
            }
            statlist(ls);  /* `then' part */
            leaveblock(fs);
            if (ls.t.token == (int)TK_ELSE ||
                ls.t.token == (int)TK_ELSEIF)  /* followed by 'else'/'elseif'? */
                luaK_concat(fs, ref escapelist, luaK_jump(fs));  /* must jump over it */
            luaK_patchtohere(fs, jf);
        }
        
        static void ifstat (Lexer ls, int line) {
            /* ifstat -> IF cond THEN block {ELSEIF cond THEN block} [ELSE block] END */
            FuncState fs = ls.fs;
            int escapelist = NO_JUMP;  /* exit list for finished parts */
            test_then_block(ls, ref escapelist);  /* IF cond THEN block */
            while (ls.t.token == (int)TK_ELSEIF)
                test_then_block(ls, ref escapelist);  /* ELSEIF cond THEN block */
            if (testnext(ls, (int)TK_ELSE))
                block(ls);  /* `else' part */
            check_match(ls, (int)TK_END, (int)TK_IF, line);
            luaK_patchtohere(fs, escapelist);  /* patch escape list to 'if' end */
        }
        
        static void localfunc (Lexer ls) {
            expdesc b = new expdesc();
            FuncState fs = ls.fs;
            new_localvar(ls, str_checkname(ls));  /* new local variable */
            adjustlocalvars(ls, 1);  /* enter its scope */
            body(ls, ref b, false, ls.linenumber);  /* function created in next register */
            /* debug information will only see the variable after this point! */
            getlocvar(fs, b.u.info).StartPC = fs.pc;
        }
        
        static void localstat (Lexer ls) {
            /* stat -> LOCAL NAME {`,' NAME} [`=' explist] */
            int nvars = 0;
            int nexps;
            expdesc e = new expdesc();
            do {
                new_localvar(ls, str_checkname(ls));
                nvars++;
            } while (testnext(ls, ','));
            if (testnext(ls, '='))
                nexps = explist(ls, ref e);
            else {
                e.k = VVOID;
                nexps = 0;
            }
            adjust_assign(ls, nvars, nexps, ref e);
            adjustlocalvars(ls, nvars);
        }
        
        static bool funcname (Lexer ls, ref expdesc v) {
            /* funcname -> NAME {fieldsel} [`:' NAME] */
            bool ismethod = false;
            singlevar(ls, ref v);
            while (ls.t.token == '.')
                fieldsel(ls, ref v);
            if (ls.t.token == ':') {
                ismethod = true;
                fieldsel(ls, ref v);
            }
            return ismethod;
        }
        
        static void funcstat (Lexer ls, int line) {
            /* funcstat -> FUNCTION funcname body */
            bool ismethod;
            expdesc v = new expdesc(), b = new expdesc();
            ls.luaX_next();  /* skip FUNCTION */
            ismethod = funcname(ls, ref v);
            body(ls, ref b, ismethod, line);
            luaK_storevar(ls.fs, ref v, ref b);
            luaK_fixline(ls.fs, line);  /* definition `happens' in the first line */
        }
        
        static void exprstat (Lexer ls) {
            /* stat -> func | assignment */
            FuncState fs = ls.fs;
            var v = new LHS_assign();
            suffixedexp(ls, ref v.v);
            if (ls.t.token == '=' || ls.t.token == ',') { /* stat -> assignment ? */
                v.prev = null;
                assignment(ls, v, 1);
            }
            else {  /* stat -> func */
                check_condition(ls, v.v.k == VCALL, "syntax error");
                getcode(fs, v.v).C = 1;/* call statement uses no results */
            }
        }
        
        static void retstat (Lexer ls) {
            /* stat . RETURN [explist] [';'] */
            FuncState fs = ls.fs;
            expdesc e = new expdesc();
            int first, nret;  /* registers with returned values */
            if (block_follow(ls, true) || ls.t.token == ';')
                first = nret = 0;  /* return no values */
            else {
                nret = explist(ls, ref e);  /* optional return values */
                if (hasmultret(e.k)) {
                    luaK_setmultret(fs, ref e);
                    if (e.k == VCALL && nret == 1) {  /* tail call? */
                        getcode(fs, e).Op = LuaOps.TAILCALL;
                        LAssert(getcode(fs,e).A == fs.nactvar);
                    }
                    first = fs.nactvar;
                    nret = LUA_MULTRET;  /* return all values */
                }
                else {
                    if (nret == 1)  /* only one single value? */
                        first = luaK_exp2anyreg(fs, ref e);
                    else {
                        luaK_exp2nextreg(fs, ref e);  /* values must go to the `stack' */
                        first = fs.nactvar;  /* return all `active' values */
                        LAssert(nret == fs.freereg - first);
                    }
                }
            }
            luaK_ret(fs, first, nret);
            testnext(ls, ';');  /* skip optional semicolon */
        }

        
        static void statement (Lexer ls) {
            int line = ls.linenumber;  /* may be needed for error messages */
            enterlevel(ls);
            switch ((RESERVED)ls.t.token) {
                case (RESERVED)';': {  /* stat -> ';' (empty statement) */
                    ls.luaX_next();  /* skip ';' */
                    break;
                }
                case TK_IF: {  /* stat -> ifstat */
                    ifstat(ls, line);
                    break;
                }
                case TK_WHILE: {  /* stat -> whilestat */
                    whilestat(ls, line);
                    break;
                }
                case TK_DO: {  /* stat -> DO block END */
                    ls.luaX_next();  /* skip DO */
                    block(ls);
                    check_match(ls, (int)TK_END, (int)TK_DO, line);
                    break;
                }
                case TK_FOR: {  /* stat -> forstat */
                    forstat(ls, line);
                    break;
                }
                case TK_REPEAT: {  /* stat -> repeatstat */
                    repeatstat(ls, line);
                    break;
                }
                case TK_FUNCTION: {  /* stat -> funcstat */
                    funcstat(ls, line);
                    break;
                }
                case TK_LOCAL: {  /* stat -> localstat */
                    ls.luaX_next();  /* skip LOCAL */
                    if (testnext(ls, (int)TK_FUNCTION))  /* local function? */
                        localfunc(ls);
                    else
                        localstat(ls);
                    break;
                }
                case TK_DBCOLON: {  /* stat -> label */
                    ls.luaX_next();  /* skip double colon */
                    labelstat(ls, str_checkname(ls), line);
                    break;
                }
                case TK_RETURN: {  /* stat -> retstat */
                    ls.luaX_next();  /* skip RETURN */
                    retstat(ls);
                    break;
                }
                case TK_BREAK:   /* stat -> breakstat */
                case TK_GOTO: {  /* stat -> 'goto' NAME */
                    gotostat(ls, luaK_jump(ls.fs));
                    break;
                }
                default: {  /* stat -> func | assignment */
                    exprstat(ls);
                    break;
                }
            }
            LAssert(ls.fs.f.MaxStackSize >= ls.fs.freereg &&
                       ls.fs.freereg >= ls.fs.nactvar);
            ls.fs.freereg = ls.fs.nactvar;  /* free registers */
            leavelevel(ls);
        }

        static void mainfunc(Lexer ls, FuncState fs)
        {
            BlockCnt bl;
            expdesc v = new expdesc();
            open_func(ls, fs, out bl);
            fs.f.IsVararg = true;
            init_exp(ref v, VLOCAL, 0);
            newupvalue(fs, ls.envn, ref v);
            ls.luaX_next();
            statlist(ls);
            check(ls, (int)TK_EOS);
            close_func(ls);
        }

        public static LuaPrototype Compile(TextReader reader, string name, int firstchar)
        {
            var funcstate = new FuncState();
            var lexstate = new Lexer();
            
            funcstate.f = new CompilerPrototype();
            funcstate.f.Source = name;
            lexstate.buffer = new StringBuilder();
            //dyndata
            lexstate.dyd = new Dyndata();
            lexstate.dyd.actvar = new List<Vardesc>();
            lexstate.dyd.gt.arr = new RefList<Labeldesc>();
            lexstate.dyd.label.arr = new RefList<Labeldesc>();
            //
            lexstate.luaX_setinput(reader,name, firstchar);
            mainfunc(lexstate, funcstate);
            LAssert(funcstate.prev == null && funcstate.nups == 1 && lexstate.fs == null);
            LAssert(lexstate.dyd.actvar.Count == 0 &&
                    lexstate.dyd.gt.arr.Count == 0 &&
                    lexstate.dyd.label.arr.Count == 0);
            return funcstate.f.ToProto();
        }
        
    }
}