using System;
using PasifeLua.Bytecode;
//using statics
using static PasifeLua.luac.expkind;
using static PasifeLua.Constants;
using static PasifeLua.Utils;
using static PasifeLua.luac.UnOpr;
using static PasifeLua.luac.BinOpr;

namespace PasifeLua.luac
{
    
    enum BinOpr {
        OPR_ADD, OPR_SUB, OPR_MUL, OPR_DIV, OPR_MOD, OPR_POW,
        OPR_CONCAT,
        OPR_EQ, OPR_LT, OPR_LE,
        OPR_NE, OPR_GT, OPR_GE,
        OPR_AND, OPR_OR,
        OPR_NOBINOPR
    }

    enum UnOpr
    {
        OPR_MINUS, OPR_NOT, OPR_LEN, OPR_NOUNOPR
    }

    static class lcode
    {
        static Instruction CREATE_ABC(LuaOps op, int a, int b, int c) => new Instruction() {Op = op, A = a, B = b, C = c};


        static bool hasjumps(expdesc e) => e.t != e.f;

        static bool isnumeral(ref expdesc e) {
            return (e.k == VKNUM && e.t == NO_JUMP && e.f == NO_JUMP);
        }
        public static void luaK_jumpto(FuncState fs, int t) => luaK_patchlist(fs, luaK_jump(fs), t);
        public static ref Instruction getcode(FuncState fs, expdesc e)
        {
            return ref fs.f.Code[e.u.info];
        }

        public static void luaK_nil(FuncState fs, int from, int n)
        {
            int l = from + n - 1;
            if (fs.pc > fs.lasttarget)
            {
                ref Instruction previous = ref fs.f.Code[fs.pc - 1];
                if (previous.Op == LuaOps.LOADNIL)
                {
                    int pfrom = previous.A;
                    int pl = pfrom + previous.B;
                    if ((pfrom <= from && from <= pl + 1) ||
                        (from <= pfrom && pfrom <= l + 1)) {  /* can connect both? */
                        if (pfrom < from) from = pfrom;  /* from = min(from, pfrom) */
                        if (pl > l) l = pl;  /* l = max(l, pl) */
                        previous.A = from;
                        previous.B = from;
                        return;
                    }
                }
            }

            luaK_codeABC(fs, LuaOps.LOADNIL, from, n - 1, 0); //else no optimisation
        }

        public static int luaK_jump(FuncState fs)
        {
            int jpc = fs.jpc;
            fs.jpc = NO_JUMP;
            int j = luaK_codeAsBx(fs, LuaOps.JMP, 0, NO_JUMP);
            luaK_concat(fs, ref j, jpc);
            return j;
        }
        
        public static void luaK_ret(FuncState fs, int first, int nret)
        {
            luaK_codeABC(fs, LuaOps.RETURN, first, nret + 1, 0);
        }

        static int condjump(FuncState fs, LuaOps op, int A, int B, int C)
        {
            luaK_codeABC(fs, op, A, B, C);
            return luaK_jump(fs);
        }

        static void fixjump(FuncState fs, int pc, int dest)
        {
            int offset = dest - (pc + 1);
            LAssert(dest != NO_JUMP);
            if(Math.Abs(offset) > Instruction.sBx_MAX)
                fs.ls.luaX_syntaxerror("control structure too long");
            fs.f.Code[pc].sBx = offset;
        }
        
        /*
        ** returns current `pc' and marks it as a jump target (to avoid wrong
        ** optimizations with consecutive instructions not in the same basic block).
        */
        public static int luaK_getlabel(FuncState fs)
        {
            fs.lasttarget = fs.pc;
            return fs.pc;
        }
        
        static int getjump(FuncState fs, int pc)
        {
            int offset = fs.f.Code[pc].sBx;
            if (offset == NO_JUMP)
                return NO_JUMP; //end of list
            else
                return (pc + 1) + offset; //turn offset into absolute position
        }

        static ref Instruction getjumpcontrol(FuncState fs, int pc)
        {
            if (pc >= 1 && OpCodeInfo.GetTMode(fs.f.Code[pc - 1].Op))
                return ref fs.f.Code[pc - 1];
            else
                return ref fs.f.Code[pc];
        }
        
        /*
        ** check whether list has any jump that do not produce a value
        ** (or produce an inverted value)
        */
        static bool need_value(FuncState fs, int list)
        {
            for (; list != NO_JUMP; list = getjump(fs, list))
            {
                if (getjumpcontrol(fs, list).Op != LuaOps.TESTSET) return true;
            }
            return false;
        }


        static bool patchtestreg(FuncState fs, int node, int reg)
        {
            ref Instruction i = ref getjumpcontrol(fs, node);
            if (i.Op != LuaOps.TESTSET)
                return false; //cannot patch other instructions
            if (reg != NO_REG && reg != i.B)
                i.A = reg;
            else
                i = CREATE_ABC(LuaOps.TEST, i.B, 0, i.C);
            return true;
        }

        static void removevalues(FuncState fs, int list)
        {
            for (; list != NO_JUMP; list = getjump(fs, list))
                patchtestreg(fs, list, NO_REG);
        }

        static void patchlistaux(FuncState fs, int list, int vtarget, int reg, int dtarget)
        {
            while (list != NO_JUMP)
            {
                int next = getjump(fs, list);
                if (patchtestreg(fs, list, reg))
                    fixjump(fs, list, vtarget);
                else
                    fixjump(fs, list, dtarget); //jump to default target
                list = next;
            }
                
        }
        
        static void dischargejpc(FuncState fs)
        {
            patchlistaux(fs, fs.jpc, fs.pc, NO_REG, fs.pc);
            fs.jpc = NO_JUMP;
        }

        public static void luaK_patchlist(FuncState fs, int list, int target)
        {
            if (target == fs.pc)
                luaK_patchtohere(fs, list);
            else {
                LAssert(target < fs.pc);
                patchlistaux(fs, list, target, NO_REG, target);
            }
        }
        
        public static void luaK_patchclose(FuncState fs, int list, int level)
        {
            level++;
            while (list != NO_JUMP)
            {
                int next = getjump(fs, list);
                ref Instruction c = ref fs.f.Code[list];
                LAssert(c.Op == LuaOps.JMP && (c.A == 0 || c.A >= level));
                c.A = level;
                list = next;
            }
        }

        public static void luaK_patchtohere(FuncState fs, int list)
        {
            luaK_getlabel(fs);
            luaK_concat(fs, ref fs.jpc, list);
        }
        public static void luaK_concat(FuncState fs, ref int l1, int l2)
        {
            if (l2 == NO_JUMP) return;
            else if (l1 == NO_JUMP)
                l1 = l2;
            else
            {
                int list = l1;
                int next;
                while ((next = getjump(fs, list)) != NO_JUMP)
                    list = next;
                fixjump(fs, list, l2);
            }
        }
        static int luaK_code(FuncState fs, Instruction i)
        {
            dischargejpc(fs);
            fs.f.Code.Add(i);
            fs.f.LineInfo.Add(fs.ls.lastline);
            return fs.pc++;
        }

        public static int luaK_codeABC(FuncState fs, LuaOps o, int a, int b, int c)
        {
            LAssert(OpCodeInfo.GetMode(o) == OpMode.iABC);
            LAssert(OpCodeInfo.GetBMode(o) != OpArgMode.N || b == 0);
            LAssert(OpCodeInfo.GetCMode(o) != OpArgMode.N || c == 0);
            LAssert(a <= MAXARG_A && b <= MAXARG_B && c <= MAXARG_C);
            return luaK_code(fs, CREATE_ABC(o, a, b, c));
        }

        public static int luaK_codeAsBx(FuncState fs, LuaOps o, int a, int sBx) =>
            luaK_codeABx(fs, o, a, (uint)(sBx + MAXARG_sBx));
        public static int luaK_codeABx(FuncState fs, LuaOps o, int a, uint bc)
        {
            LAssert(OpCodeInfo.GetMode(o) == OpMode.iABx || OpCodeInfo.GetMode(o) == OpMode.iAsBx);
            LAssert(OpCodeInfo.GetCMode(o) == OpArgMode.N);
            LAssert(a <= MAXARG_A && bc <= MAXARG_Bx);
            return luaK_code(fs, new Instruction() {Op = o, A = a, Bx = bc});
        }

        static int codeextraarg(FuncState fs, int a)
        {
            LAssert(a <= MAXARG_Ax);
            return luaK_code(fs, new Instruction() {Op = LuaOps.EXTRAARG, Ax =  a});
        }

        public static int luaK_codek(FuncState fs, int reg, int k)
        {
            if (k <= MAXARG_Bx)
                return luaK_codeABx(fs, LuaOps.LOADK, reg, (uint)k);
            else
            {
                int p = luaK_codeABx(fs, LuaOps.LOADKX, reg, 0);
                codeextraarg(fs, k);
                return p;
            }
        }

        public static void luaK_checkstack(FuncState fs, int n)
        {
            int newstack = fs.freereg + n;
            if (newstack > fs.f.MaxStackSize)
            {
                if(newstack >= MAXSTACK)
                    fs.ls.luaX_syntaxerror("function or expression too complex");
                fs.f.MaxStackSize = (byte)newstack;
            }
        }

        public static void luaK_reserveregs(FuncState fs, int n)
        {
            luaK_checkstack(fs, n);
            fs.freereg += (byte)n;
        }

        static void freereg(FuncState fs, int reg)
        {
            if (!ISK(reg) && reg >= fs.nactvar)
            {
                fs.freereg--;
                LAssert(reg == fs.freereg);
            }
        }

        static void freeexp(FuncState fs, ref expdesc e)
        {
            if (e.k == VNONRELOC)
                freereg(fs, e.u.info);
        }

        static int addk(FuncState fs, LuaValue v)
        {
            if (!fs.h.TryGetValue(v, out int k))
            {
                k = fs.f.Constants.Count;
                fs.f.Constants.Add(v);
                fs.h.Add(v, k);
                fs.nk++;
            }
            return k;
        }

        public static int luaK_stringK(FuncState fs, string s)
        {
            return addk(fs, new LuaValue(LuaType.String, s));
        }

        public static int luaK_numberK(FuncState fs, double r)
        {
            return addk(fs, new LuaValue(LuaType.Number, r));
        }

        static int boolK(FuncState fs, bool b)
        {
            return addk(fs, new LuaValue(b));
        }

        static int nilK(FuncState fs)
        {
            return addk(fs, new LuaValue(LuaType.Nil));
        }

        public static void luaK_setmultret(FuncState fs, ref expdesc e)
        {
            luaK_setreturns(fs, ref e, LUA_MULTRET);
        }
        
        public static void luaK_setreturns(FuncState fs, ref expdesc e, int nresults)
        {
            if (e.k == VCALL) { //expression is an open function call?
                getcode(fs, e).C = nresults + 1;
            }
            else if (e.k == VVARARG) {
                
                getcode(fs, e).B = nresults + 1;
                getcode(fs, e).A = fs.freereg;
                luaK_reserveregs(fs, 1);
            }
        }

        public static void luaK_setoneret(FuncState fs, ref expdesc e)
        {
            if (e.k == VCALL) { //open function call
                e.k = VNONRELOC;
                e.u.info = getcode(fs, e).A;
            } else if(e.k == VVARARG)
            {
                getcode(fs, e).B = 2;
                e.k = VRELOCABLE; //can relocate its simple results
            }
        }

        public static void luaK_dischargevars(FuncState fs, ref expdesc e)
        {
            switch (e.k)
            {
                case VLOCAL:
                {
                    e.k = VNONRELOC;
                    break;
                }
                case VUPVAL:
                {
                    e.u.info = luaK_codeABC(fs, LuaOps.GETUPVAL, 0, e.u.info, 0);
                    e.k = VRELOCABLE;
                    break;
                }
                case VINDEXED:
                {
                    LuaOps op = LuaOps.GETTABUP; //assume t is in an upvalue
                    freereg(fs, e.u.ind.idx);
                    if (e.u.ind.vt == (byte)VLOCAL) { //t is in a register?
                        freereg(fs, e.u.ind.t);
                        op = LuaOps.GETTABLE;
                    }
                    e.u.info = luaK_codeABC(fs, op, 0, e.u.ind.t, e.u.ind.idx);
                    e.k = VRELOCABLE;
                    break;
                }
                case VVARARG:
                case VCALL:
                {
                    luaK_setoneret(fs, ref e);
                    break;
                }
                default: break;
            }
        }

        static int code_label(FuncState fs, int A, int b, int jump)
        {
            luaK_getlabel(fs);
            return luaK_codeABC(fs, LuaOps.LOADBOOL, A, b, jump);
        }

        static void discharge2reg(FuncState fs, ref expdesc e, int reg)
        {
            luaK_dischargevars(fs, ref e);
            switch (e.k) {
                case VNIL:
                    luaK_nil(fs, reg, 1);
                    break;
                case VFALSE:
                case VTRUE:
                    luaK_codeABC(fs, LuaOps.LOADBOOL, reg, e.k == VTRUE ? 1 : 0, 0);
                    break;
                case VK:
                    luaK_codek(fs, reg, e.u.info);
                    break;
                case VKNUM:
                    luaK_codek(fs, reg, luaK_numberK(fs, e.u.nval));
                    break;
                case VRELOCABLE:
                    getcode(fs, e).A = reg;
                    break;
                case VNONRELOC:
                    if (reg != e.u.info)
                        luaK_codeABC(fs, LuaOps.MOVE, reg, e.u.info, 0);
                    break;
                default:
                    LAssert(e.k == VVOID || e.k == VJMP);
                    return;
            }
            e.u.info = reg;
            e.k = VNONRELOC;
        }

        static void discharge2anyreg(FuncState fs, ref expdesc e)
        {
            if (e.k != VNONRELOC)
            {
                luaK_reserveregs(fs, 1);
                discharge2reg(fs, ref e, fs.freereg - 1);
            }
        }
        
        static void exp2reg (FuncState fs, ref expdesc e, int reg) 
        {
            discharge2reg(fs, ref e, reg);
            if (e.k == VJMP)
                luaK_concat(fs, ref e.t, e.u.info);  /* put this jump in `t' list */
            if (hasjumps(e)) {
                int final;  /* position after whole expression */
                int p_f = NO_JUMP;  /* position of an eventual LOAD false */
                int p_t = NO_JUMP;  /* position of an eventual LOAD true */
                if (need_value(fs, e.t) || need_value(fs, e.f)) {
                    int fj = (e.k == VJMP) ? NO_JUMP : luaK_jump(fs);
                    p_f = code_label(fs, reg, 0, 1);
                    p_t = code_label(fs, reg, 1, 0);
                    luaK_patchtohere(fs, fj);
                }
                final = luaK_getlabel(fs);
                patchlistaux(fs, e.f, final, reg, p_f);
                patchlistaux(fs, e.t, final, reg, p_t);
            }
            e.f = e.t = NO_JUMP;
            e.u.info = reg;
            e.k = VNONRELOC;
        }
        
        public static void luaK_exp2nextreg (FuncState fs, ref expdesc e) 
        {
            luaK_dischargevars(fs, ref e);
            freeexp(fs, ref e);
            luaK_reserveregs(fs, 1);
            exp2reg(fs, ref e, fs.freereg - 1);
        }
        
        public static int luaK_exp2anyreg (FuncState fs, ref expdesc e) 
        {
            luaK_dischargevars(fs, ref e);
            if (e.k == VNONRELOC) {
                if (!hasjumps(e)) return e.u.info;  /* exp is already in a register */
                if (e.u.info >= fs.nactvar) {  /* reg. is not a local? */
                    exp2reg(fs, ref e, e.u.info);  /* put value on it */
                    return e.u.info;
                }
            }
            luaK_exp2nextreg(fs, ref e);  /* default */
            return e.u.info;
        }
        
        public static void luaK_exp2anyregup (FuncState fs, ref expdesc e) {
            if (e.k != VUPVAL || hasjumps(e))
                luaK_exp2anyreg(fs, ref e);
        }
        
        public static void luaK_exp2val (FuncState fs, ref expdesc e) {
            if (hasjumps(e))
                luaK_exp2anyreg(fs, ref e);
            else
                luaK_dischargevars(fs, ref e);
        }
        
        public static int luaK_exp2RK (FuncState fs, ref expdesc e) {
            luaK_exp2val(fs, ref e);
            switch (e.k) {
                case VTRUE:
                case VFALSE:
                case VNIL: {
                    if (fs.nk <= MAXINDEXRK) {  /* constant fits in RK operand? */
                        e.u.info = (e.k == VNIL) ? nilK(fs) : boolK(fs, (e.k == VTRUE));
                        e.k = VK;
                        return RKASK(e.u.info);
                    }
                    else break;
                }
                case VKNUM: {
                    e.u.info = luaK_numberK(fs, e.u.nval);
                    e.k = VK;
                    /* go through */
                    goto case VK;
                }
                case VK: {
                    if (e.u.info <= MAXINDEXRK)  /* constant fits in argC? */
                        return RKASK(e.u.info);
                    else break;
                }
                default: break;
            }
            /* not a constant in the right range: put it in a register */
            return luaK_exp2anyreg(fs, ref e);
        }
        
        public static void luaK_storevar (FuncState fs, ref expdesc var, ref expdesc ex) {
            switch (var.k) {
                case VLOCAL: {
                    freeexp(fs, ref ex);
                    exp2reg(fs, ref ex, var.u.info);
                    return;
                }
                case VUPVAL: {
                    int e = luaK_exp2anyreg(fs, ref ex);
                    luaK_codeABC(fs, LuaOps.SETUPVAL, e, var.u.info, 0);
                    break;
                }
                case VINDEXED: {
                    LuaOps op = (var.u.ind.vt == (byte)VLOCAL) ? LuaOps.SETTABLE : LuaOps.SETTABUP;
                    int e = luaK_exp2RK(fs, ref ex);
                    luaK_codeABC(fs, op, var.u.ind.t, var.u.ind.idx, e);
                    break;
                }
                default: {
                    LAssert(false);  /* invalid var kind to store */
                    break;
                }
            }
            freeexp(fs, ref ex);
        }
        
        public static void luaK_self (FuncState fs, ref expdesc e, ref expdesc key) {
            int ereg;
            luaK_exp2anyreg(fs, ref e);
            ereg = e.u.info;  /* register where 'e' was placed */
            freeexp(fs, ref e);
            e.u.info = fs.freereg;  /* base register for op_self */
            e.k = VNONRELOC;
            luaK_reserveregs(fs, 2);  /* function and 'self' produced by op_self */
            luaK_codeABC(fs, LuaOps.SELF, e.u.info, ereg, luaK_exp2RK(fs, ref key));
            freeexp(fs, ref key);
        }

        static void invertjump(FuncState fs, ref expdesc e)
        {
            ref Instruction pc = ref getjumpcontrol(fs, e.u.info);
            LAssert(OpCodeInfo.GetTMode(pc.Op) && pc.Op != LuaOps.TEST && pc.Op != LuaOps.TESTSET);
            pc.A = pc.A > 0 ? 0 : 1;
        }
        
        static int jumponcond (FuncState fs, ref expdesc e, bool cond) {
            if (e.k == VRELOCABLE) {
                Instruction ie = getcode(fs, e);
                if (ie.Op == LuaOps.NOT) {
                    fs.pc--;  /* remove previous OP_NOT */
                    return condjump(fs, LuaOps.TEST, ie.B, 0, cond ? 0 :1);
                }
                /* else go through */
            }
            discharge2anyreg(fs, ref e);
            freeexp(fs, ref e);
            return condjump(fs, LuaOps.TESTSET, NO_REG, e.u.info, cond ? 1 : 0);
        }
        
        public static void luaK_goiftrue (FuncState fs, ref expdesc e) {
            int pc;  /* pc of last jump */
            luaK_dischargevars(fs, ref e);
            switch (e.k) {
                case VJMP: {
                    invertjump(fs, ref e);
                    pc = e.u.info;
                    break;
                }
                case VK: case VKNUM: case VTRUE: {
                    pc = NO_JUMP;  /* always true; do nothing */
                    break;
                }
                default: {
                    pc = jumponcond(fs, ref e, false);
                    break;
                }
            }
            luaK_concat(fs, ref e.f, pc);  /* insert last jump in `f' list */
            luaK_patchtohere(fs, e.t);
            e.t = NO_JUMP;
        }
        
        public static void luaK_goiffalse (FuncState fs, ref expdesc e) {
            int pc;  /* pc of last jump */
            luaK_dischargevars(fs, ref e);
            switch (e.k) {
                case VJMP: {
                    pc = e.u.info;
                    break;
                }
                case VNIL: case VFALSE: {
                    pc = NO_JUMP;  /* always false; do nothing */
                    break;
                }
                default: {
                    pc = jumponcond(fs, ref e, true);
                    break;
                }
            }
            luaK_concat(fs, ref e.t, pc);  /* insert last jump in `t' list */
            luaK_patchtohere(fs, e.f);
            e.f = NO_JUMP;
        }
        
        static void codenot (FuncState fs, ref expdesc e) {
            luaK_dischargevars(fs, ref e);
            switch (e.k) {
                case VNIL: case VFALSE: {
                    e.k = VTRUE;
                    break;
                }
                case VK: case VKNUM: case VTRUE: {
                    e.k = VFALSE;
                    break;
                }
                case VJMP: {
                    invertjump(fs, ref e);
                    break;
                }
                case VRELOCABLE:
                case VNONRELOC: {
                    discharge2anyreg(fs, ref e);
                    freeexp(fs, ref e);
                    e.u.info = luaK_codeABC(fs, LuaOps.NOT, 0, e.u.info, 0);
                    e.k = VRELOCABLE;
                    break;
                }
                default: {
                    LAssert(false);  /* cannot happen */
                    break;
                }
            }
            /* interchange true and false lists */
            { int temp = e.f; e.f = e.t; e.t = temp; }
            removevalues(fs, e.f);
            removevalues(fs, e.t);
        }
        
        public static void luaK_indexed (FuncState fs, ref expdesc t, ref expdesc k) {
            LAssert(!hasjumps(t));
            t.u.ind.t = (byte)t.u.info;
            t.u.ind.idx = (short)luaK_exp2RK(fs, ref k);
            t.u.ind.vt = (byte) (
                (t.k == VUPVAL)
                    ? VUPVAL
                    : check_exp(vkisinreg((int) t.k), VLOCAL)
            );
            t.k = VINDEXED;
        }

        static double EvalConst(LuaOps op, double a, double b)
        {
            switch (op) {
                case LuaOps.ADD:
                    return a + b;
                case LuaOps.SUB:
                    return a - b;
                case LuaOps.MUL:
                    return a * b;
                case LuaOps.DIV:
                    return a / b;
                case LuaOps.MOD:
                    return a % b;
                case LuaOps.POW:
                    return Math.Pow(a, b);
                default:
                    throw new Exception();
            }
        }

        static bool constfolding(LuaOps op, ref expdesc e1, ref expdesc e2)
        {
            if (!isnumeral(ref e1) || !isnumeral(ref e2)) return false;
            if ((op == LuaOps.DIV || op == LuaOps.MOD) && e2.u.nval == 0)
                return false;
            e1.u.nval = EvalConst(op, e1.u.nval, e2.u.nval);
            return true;
        }
        
        static void codearith (FuncState fs, LuaOps op,
            ref expdesc e1, ref expdesc e2, int line) {
            if (constfolding(op, ref e1, ref e2))
                return;
            else {
                int o2 = (op != LuaOps.UNM && op != LuaOps.LEN) ? luaK_exp2RK(fs, ref e2) : 0;
                int o1 = luaK_exp2RK(fs, ref e1);
                if (o1 > o2) {
                    freeexp(fs, ref e1);
                    freeexp(fs, ref e2);
                }
                else {
                    freeexp(fs, ref e2);
                    freeexp(fs, ref e1);
                }
                e1.u.info = luaK_codeABC(fs, op, 0, o1, o2);
                e1.k = VRELOCABLE;
                luaK_fixline(fs, line);
            }
        }
        
        static void codecomp (FuncState fs, LuaOps op, int cond, ref expdesc e1, ref expdesc e2) 
        {
            int o1 = luaK_exp2RK(fs, ref e1);
            int o2 = luaK_exp2RK(fs, ref e2);
            freeexp(fs, ref e2);
            freeexp(fs, ref e1);
            if (cond == 0 && op != LuaOps.EQ) {
                int temp;  /* exchange args to replace by `<' or `<=' */
                temp = o1; o1 = o2; o2 = temp;  /* o1 <==> o2 */
                cond = 1;
            }
            e1.u.info = condjump(fs, op, cond, o1, o2);
            e1.k = VJMP;
        }
        
        public static void luaK_prefix (FuncState fs, UnOpr op, ref expdesc e, int line)
        {
            expdesc e2 = new expdesc();
            e2.t = e2.f = NO_JUMP; e2.k = VKNUM; e2.u.nval = 0;
            switch (op) {
                case OPR_MINUS: {
                    if (isnumeral(ref e))  /* minus constant? */
                        e.u.nval = -e.u.nval;  /* fold it */
                    else {
                        luaK_exp2anyreg(fs, ref e);
                        codearith(fs, LuaOps.UNM, ref e, ref e2, line);
                    }
                    break;
                }
                case OPR_NOT: codenot(fs, ref e); break;
                case OPR_LEN: {
                    luaK_exp2anyreg(fs, ref e);  /* cannot operate on constants */
                    codearith(fs, LuaOps.LEN, ref e, ref e2, line);
                    break;
                }
                default: throw new Exception();
            }
        }
        
        public static void luaK_infix (FuncState fs, BinOpr op, ref expdesc v) {
            switch (op) {
                case OPR_AND: {
                    luaK_goiftrue(fs, ref v);
                    break;
                }
                case OPR_OR: {
                    luaK_goiffalse(fs, ref v);
                    break;
                }
                case OPR_CONCAT: {
                    luaK_exp2nextreg(fs, ref v);  /* operand must be on the `stack' */
                    break;
                }
                case OPR_ADD: case OPR_SUB: case OPR_MUL: case OPR_DIV:
                case OPR_MOD: case OPR_POW: {
                    if (!isnumeral(ref v)) luaK_exp2RK(fs, ref v);
                    break;
                }
                default: {
                    luaK_exp2RK(fs, ref v);
                    break;
                }
            }
        }
        
        public static void luaK_posfix (FuncState fs, BinOpr op,
            ref expdesc e1, ref expdesc e2, int line) {
            switch (op) {
                case OPR_AND: {
                    LAssert(e1.t == NO_JUMP);  /* list must be closed */
                    luaK_dischargevars(fs, ref e2);
                    luaK_concat(fs, ref e2.f, e1.f);
                    e1 = e2;
                    break;
                }
                case OPR_OR: {
                    LAssert(e1.f == NO_JUMP);  /* list must be closed */
                    luaK_dischargevars(fs, ref e2);
                    luaK_concat(fs, ref e2.t, e1.t);
                    e1 = e2;
                    break;
                }
                case OPR_CONCAT: {
                    luaK_exp2val(fs, ref e2);
                    if (e2.k == VRELOCABLE && getcode(fs, e2).Op == LuaOps.CONCAT) {
                        LAssert(e1.u.info == (getcode(fs, e2).B)-1);
                        freeexp(fs, ref e1);
                        getcode(fs, e2).B = e1.u.info;
                        e1.k = VRELOCABLE; e1.u.info = e2.u.info;
                    }
                    else {
                        luaK_exp2nextreg(fs, ref e2);  /* operand must be on the 'stack' */
                        codearith(fs, LuaOps.CONCAT, ref e1, ref e2, line);
                    }
                    break;
                }
                case OPR_ADD: case OPR_SUB: case OPR_MUL: case OPR_DIV:
                case OPR_MOD: case OPR_POW: {
                    codearith(fs, ( op - OPR_ADD + LuaOps.ADD), ref e1, ref e2, line);
                    break;
                }
                case OPR_EQ: case OPR_LT: case OPR_LE: {
                    codecomp(fs, (op - OPR_EQ + LuaOps.EQ), 1, ref e1, ref e2);
                    break;
                }
                case OPR_NE: case OPR_GT: case OPR_GE: {
                    codecomp(fs, (op - OPR_NE + LuaOps.EQ), 0, ref e1, ref e2);
                    break;
                }
                default: throw new Exception();
            }
        }
        
        public static void luaK_fixline(FuncState fs, int line)
        {
            fs.f.LineInfo[fs.pc - 1] = line;
        }
        
        public static void luaK_setlist (FuncState fs, int _base, int nelems, int tostore) {
            int c =  (nelems - 1)/LFIELDS_PER_FLUSH + 1;
            int b = (tostore == LUA_MULTRET) ? 0 : tostore;
            LAssert(tostore != 0);
            if (c <= MAXARG_C)
                luaK_codeABC(fs, LuaOps.SETLIST, _base, b, c);
            else if (c <= MAXARG_Ax) {
                luaK_codeABC(fs, LuaOps.SETLIST, _base, b, 0);
                codeextraarg(fs, c);
            }
            else
                fs.ls.luaX_syntaxerror( "constructor too long");
            fs.freereg = (byte)(_base + 1);  /* free registers with list values */
        }
    }
}