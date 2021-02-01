using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PasifeLua.Bytecode;
using PasifeLua.Interop;
using static PasifeLua.Utils;
using static PasifeLua.Constants;

namespace PasifeLua
{
    public partial class LuaState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static LuaValue RKB(Instruction i, LuaPrototype fn, Span<LuaValue> stack)
        {
            return i.B >= 256 ? fn.Constants[i.B - 256] : stack[i.B];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static LuaValue RKC(Instruction i, LuaPrototype fn, Span<LuaValue> stack)
        {
            return i.C >= 256 ? fn.Constants[i.C - 256] : stack[i.C];
        }
        
        static LuaValue Concat(Span<LuaValue> stackView, int b, int c)
        {
            var str = stackView[b++].ToString();
            while (b <= c) {
                str += stackView[b++];
            }
            return new LuaValue(LuaType.String, str);
        }

        LuaFunction CreateClosure(LuaPrototype prot, int svBase, LuaFunction parent)
        {
            var func = new LuaFunction(this, prot, prot.Upvalues.Length);
            for (int i = 0; i < func.UpValues.Length; i++) {
                if (prot.Upvalues[i].Stack != 0)
                {
                    func.UpValues[i] = FindUpVal(svBase + prot.Upvalues[i].Index);
                }
                else
                {
                    func.UpValues[i] = parent.UpValues[prot.Upvalues[i].Index];
                }
            }
            return func;
        }

        int adjust_varargs(LuaPrototype p, int actual)
        {
            int nfixargs = p.NumParams;
            LAssert(actual >= nfixargs);
            CheckStack(p.MaxStackSize);
            int _base, _fixed;
            _fixed = top - actual;
            _base = top;
            for (int i = 0; i < nfixargs; i++)
            {
                _Stack[top++] = _Stack[_fixed + i];
                setnilvalue(_fixed + i);
            }
            return _base;
        }
        
         bool luaD_precall(int func, int nresults)
        {
            var v = _Stack[func];
            if(v.Type == LuaType.Nil) throw new Exception("Attempt to call nil value");
            if (v.Type == LuaType.LightUserData)
            {
                CheckStack(MINSTACK);
                ci = new CallInfo()
                {
                    Func = func,
                    nresults = nresults,
                    Top = top + MINSTACK,
                    Previous = ci
                };
                int n = v.Object<ClrFunction>().Run(this);
                api_checknelems(n);
                luaD_poscall(top - n);
                return true;
            }
            else
            {
                var closure = v.Object<LuaFunction>();
                if(closure.state != this) throw new Exception("Closure not made for this LuaState");
                var p = closure.Prototype;
                int n = top - func - 1;
                CheckStack(p.MaxStackSize);
                for (; n < p.NumParams; n++)
                    setnilvalue(top++);
                int _base;
                if (!p.IsVararg) {
                    _base = func + 1;
                }
                else {
                    _base = adjust_varargs(p, n);
                }
                ci = new CallInfo()
                {
                    nresults = nresults,
                    Func = func,
                    Base = _base,
                    Top = _base + p.MaxStackSize,
                    SavedPC = 0,
                    Previous = ci
                };
                top = ci.Top;
                return false;
            }
        }

        int luaD_poscall(int firstResult)
        {
            int i;
            int res = ci.Func;
            int wanted = ci.nresults;
            ci = ci.Previous; //back to caller
            for (i = wanted; i != 0 && firstResult < top; i--)
            {
                _Stack[res++] = _Stack[firstResult++];
            }
            while (i-- > 0)
                setnilvalue(res++);
            top = res;
            return (wanted - LUA_MULTRET);
        }

        void CallTagMethod(ref LuaValue f, ref LuaValue p1, ref LuaValue p2, int p3, bool hasres)
        {
            _Stack[top++] = f;
            _Stack[top++] = p1;
            _Stack[top++] = p2;
            if (!hasres)
                _Stack[top++] = _Stack[p3];
            int nres = hasres ? 1 : 0;
            Call(top - (4 - nres), nres, 0);
            if (hasres) {
                _Stack[p3] = _Stack[--top];
            }
        }

        internal void CallTagMethod(ref LuaValue func, ref LuaValue p1, ref LuaValue p2, ref LuaValue p3)
        {
            _Stack[top++] = func;
            _Stack[top++] = p1;
            _Stack[top++] = p2;
            _Stack[top++] = p3;
            Call(top - 4, 0, 0);
        }
        
        internal bool CallBinTM(ref LuaValue p1, ref LuaValue p2, int res, TMS ev)
        {
            LuaValue tm = TM.GetTMByObj(this, p1, ev);
            if (tm.IsNil())
                tm = TM.GetTMByObj(this, p2, ev);
            if (tm.IsNil()) return false;
            CallTagMethod(ref tm, ref p1, ref p2, res, true);
            return true;
        }

        internal bool CallOrderTM(ref LuaValue p1, ref LuaValue p2, TMS ev, out bool res)
        {
            res = false;
            if (!CallBinTM(ref p1, ref p2, top, ev))
                return false;
            res = _Stack[top].Boolean();
            return true;
        }

        bool NumberArith(TMS op, LuaValue a, LuaValue b, out double x)
        {
            x = 0;
            if (!a.TryGetNumber(out var na)) return false;
            if (!b.TryGetNumber(out var nb)) return false;
            switch (op) {
                case TMS.ADD:
                    x = na + nb;
                    break;
                case TMS.SUB:
                    x = na - nb;
                    break;
                case TMS.MUL:
                    x = na * nb;
                    break;
                case TMS.DIV:
                    x = na / nb;
                    break;
                case TMS.MOD:
                    x = na % nb;
                    break;
                case TMS.POW:
                    x = Math.Pow(na, nb);
                    break;
                case TMS.UNM:
                    x = -na;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return true;
        }

        bool Number_LE(LuaValue a, LuaValue b)
        {
            if(!a.TryGetNumber(out var na)) throw new InvalidCastException();
            if(!b.TryGetNumber(out var nb)) throw new InvalidCastException();
            return na <= nb;
        }

        
        
        void DoJump(Instruction inst, int e)
        {
            if (inst.A > 0) {
                CloseUpVals(ci.Base + inst.A - 1);
            }
            ci.SavedPC += inst.sBx + e;
        }
        void DoNextJump(LuaPrototype proto) {
            DoJump(proto.Code[ci.SavedPC], 1);
        }
        
        void BinOp(Instruction inst, LuaPrototype fn, ref Span<LuaValue> localStack, ref LuaValue[] sref, int ra, TMS tm)
        {
            var rb = RKB(inst, fn, localStack);
            var rc = RKC(inst, fn, localStack);
            if (NumberArith(tm, rb, rc, out var x)) {
                localStack[inst.A] = new LuaValue(x);
            } else {
                if(!CallBinTM(ref rb, ref rc, ra, tm))
                    throw new ArithmeticException();
                CheckStackframe(ref sref, ref localStack);
            }
        }

        void CheckStackframe(ref LuaValue[] sref, ref Span<LuaValue> localStack)
        {
            if (sref != _Stack)
            {
                sref = _Stack;
                var closure = _Stack[ci.Func].Object<LuaFunction>();
                localStack = new Span<LuaValue>(_Stack, ci.Base, closure.Prototype.MaxStackSize);
            }
        }

        static LuaValue IndexList(IList list, int index)
        {
            index = index - 1;
            if (index < 0 || index >= list.Count)
                return new LuaValue();
            if (list is IList<int> i)
                return new LuaValue(i[index]);
            else {
                return LuaValue.FromObject(list[index]);
            }
        }

        static void SetUserData(LuaValue tab, LuaValue key, LuaValue val)
        {
            TypeDescriptor td;
            if ((td = UserData.GetDescriptor(tab.obj.GetType())) != null)
            {
                td.Set(tab.obj, key, val);
            } else if (tab.obj is IList lst)
            {
                if (!key.TryGetNumber(out double idx))
                    throw new Exception("expected number for IList index");
                lst[(int) (idx - 1)] = val.Value;
            } 
            else if (tab.obj is IDictionary dict)
            {
                dict[key.Value] = val.Value;
            } else {
                throw new Exception($"Descriptor for type {tab.obj.GetType()} not present");
            }
        }
        static LuaValue IndexUserData(LuaValue tab, LuaValue key, bool self)
        {
            TypeDescriptor td;
            if ((td = UserData.GetDescriptor(tab.obj.GetType())) != null) {
                var v = td.Get(tab.obj, key, self);
                return  v;
            } else if (tab.obj is IList lst) {
                if (self || !key.TryGetNumber(out double idx)) {
                    return IListInterop.GetFunction(key);
                } else {
                    return IndexList(lst, (int) idx);
                }
            } else if (tab.obj is IDictionary dict) {
                if (self)
                {
                    return IDictionaryInterop.GetFunction(key);
                } else {
                    if (dict.Contains(key.Value)) {
                        return LuaValue.FromObject(dict[key.Value]);
                    }
                    else {
                        return new LuaValue();
                    }
                }
            } else {
                return new LuaValue(); //nil
            }
        }

        void Execute()
        {
            newframe:
            var sref = _Stack;
            var closure = _Stack[ci.Func].Object<LuaFunction>();
            var fn = closure.Prototype;
            var localStack = new Span<LuaValue>(_Stack, ci.Base, fn.MaxStackSize);
            int baseTop = ci.Base;
            
            while (ci.SavedPC < fn.Code.Length)
            {
                var inst = fn.Code[ci.SavedPC++];
                var ra = baseTop + inst.A;
                switch (inst.Op)
                {
                    /* BINARY OPERATORS */
                    case LuaOps.ADD:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.ADD);
                        break;
                    case LuaOps.SUB:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.SUB);
                        break;
                    case LuaOps.MUL:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.MUL);
                        break;
                    case LuaOps.DIV:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.DIV);
                        break;
                    case LuaOps.MOD:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.MOD);
                        break;
                    case LuaOps.POW:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.POW);
                        break;
                    case LuaOps.UNM:
                        BinOp(inst, fn, ref localStack, ref sref, ra, TMS.UNM);
                        break;
                    /* UNARY FUNCTIONS */
                    case LuaOps.MOVE:
                    {
                        localStack[inst.A] = localStack[inst.B];
                        break;
                    }
                    case LuaOps.LEN:
                    {
                        var v = localStack[inst.B];
                        if (v.Type == LuaType.String)
                            localStack[inst.A] = new LuaValue(v.ToString().Length);
                        else if (v.Type == LuaType.Table || v.Type == LuaType.UserData)
                        {
                            if (CallBinTM(ref v, ref v, ra, TMS.LEN)) {
                                CheckStackframe(ref sref, ref localStack);
                            }
                            else
                            {
                                if(v.Type == LuaType.Table)
                                    localStack[inst.A] = new LuaValue(v.Table().Length);
                                else if(v.obj is IList list) {
                                    localStack[inst.A] = new LuaValue(list.Count);
                                    CheckStackframe(ref sref, ref localStack);
                                } else if (v.obj is IDictionary dict) {
                                    localStack[inst.A] = new LuaValue(dict.Count);
                                    CheckStackframe(ref sref, ref localStack);
                                }
                                else {
                                    throw new Exception($"cannot get length for object of type {v.obj.GetType()}");
                                }
                            }
                        } 
                        else
                            throw new Exception($"attempt to get length of a {v.Type.ToString().ToLowerInvariant()} value");
                        break;
                    }
                    /* LOGICAL FUNCTIONS */
                    
                    case LuaOps.EQ:
                    {
                        bool val;
                        if (inst.A <= 0)
                            val = (!LuaValue.ValuesEqual(RKB(inst, fn, localStack), RKC(inst, fn, localStack), this));
                        else
                            val = LuaValue.ValuesEqual(RKB(inst, fn, localStack), RKC(inst, fn, localStack), this);
                        CheckStackframe(ref sref, ref localStack);
                        if (val) DoNextJump(fn);
                        else ci.SavedPC++;
                        break;
                    }
                    case LuaOps.LT:
                    {
                        var val = LuaValue.LessThan(this,
                            RKB(inst, fn, localStack),
                            RKC(inst, fn, localStack));
                        CheckStackframe(ref sref, ref localStack);
                        if (val != (inst.A > 0)) {
                            ci.SavedPC++;
                        }
                        else {
                            DoNextJump(fn);
                        }
                        break;
                    }
                    case LuaOps.LE:
                    {
                        var val = LuaValue.LessEquals(this,
                            RKB(inst, fn, localStack),
                            RKC(inst, fn, localStack));
                        CheckStackframe(ref sref, ref localStack);
                        if (val != (inst.A > 0)) {
                            ci.SavedPC++;
                        }
                        else {
                            DoNextJump(fn);
                        }
                        break;
                    }
                    case LuaOps.TEST:
                    {
                        var a = localStack[inst.A].Boolean();
                        if ((inst.C > 0) ? !a : a)
                            ci.SavedPC++;
                        else {
                            DoNextJump(fn);
                        }
                        break;
                    }
                    case LuaOps.TESTSET:
                    {
                        var b = localStack[inst.B].Boolean();
                        if ((inst.C > 0) ? !b : b)
                            ci.SavedPC++;
                        else {
                            localStack[inst.A] = localStack[inst.B];
                        }
                        break;
                    }
                    
                    /* BRANCHES, LOOPS AND CLOSURES */
                    case LuaOps.JMP:
                    {
                        DoJump(inst, 0);
                        break;
                    }
                   
                    case LuaOps.FORLOOP:
                    {
                        var step = localStack[inst.A + 2];
                        LAssert(NumberArith(TMS.ADD, localStack[inst.A], step, out var aXz));
                        var aX = new LuaValue(aXz);
                        if (0 < step.Number()  ? Number_LE(aX, localStack[inst.A + 1]) :
                                                    Number_LE(localStack[inst.A + 1], aX))
                        {
                            ci.SavedPC += inst.sBx;
                            localStack[inst.A] = aX;
                            localStack[inst.A + 3] = aX;
                        }
                        break;
                    }
                    case LuaOps.FORPREP:
                    {
                        LAssert(NumberArith(TMS.SUB, localStack[inst.A], localStack[inst.A + 2], out var x));
                        localStack[inst.A] = new LuaValue(x);
                        ci.SavedPC += inst.sBx;
                        break;
                    }
                    case LuaOps.CLOSURE:
                    {
                        localStack[inst.A] = new LuaValue(LuaType.Function, CreateClosure(fn.Protos[inst.Bx], baseTop, closure)); 
                        break;
                    }
                    
                    /* TABLE ACCESS */
                    case LuaOps.SELF:
                    {
                        localStack[inst.A + 1] = localStack[inst.B];
                        goto case LuaOps.GETTABLE;
                    }
                    case LuaOps.GETTABLE:
                    {
                        if(localStack[inst.B].IsNil())
                            throw new Exception("attempt to index nil value");
                        var oldci = ci;
                        var key = RKC(inst, fn, localStack);
                        var tab = localStack[inst.B];
                        if (tab.Type == LuaType.UserData)
                        {
                            localStack[inst.A] = IndexUserData(tab, key, inst.Op == LuaOps.SELF);
                            CheckStackframe(ref sref, ref localStack);
                        }
                        else
                        {
                            var val = tab.Table()[key];
                            if (val.IsNil())
                            {
                                if (CallBinTM(ref tab, ref key, ra, TMS.INDEX))
                                {
                                    LAssert(oldci == ci);
                                    CheckStackframe(ref sref, ref localStack);
                                    break;
                                }
                            }
                            localStack[inst.A] = val;
                        }

                        break;
                    }
                    case LuaOps.SETTABLE:
                    {
                        if(localStack[inst.A].IsNil())
                            throw new Exception("attempt to index nil value");
                        var tb = localStack[inst.A];
                        var key = RKB(inst, fn, localStack);
                        var val = RKC(inst, fn, localStack);
                        if (tb.Type == LuaType.UserData)
                        {
                            SetUserData(tb, key, val);
                        }
                        else
                        {
                            var oldci = ci;
                            var table = tb.Table();
                            table.SetValue(key, val, this);
                            LAssert(oldci == ci);
                            CheckStackframe(ref sref, ref localStack); //TM can modify stack
                        }
                        break;
                    }
                    case LuaOps.NEWTABLE:
                    {
                        localStack[inst.A] = new LuaValue(new LuaTable());
                        break;
                    }
                    case LuaOps.SETLIST:
                    {
                        var table = localStack[inst.A].Table();
                        int n = inst.B;
                        int c = inst.C;
                        if (n == 0) n = (top - ra) - 1;
                        if (c == 0)
                        {
                            if (fn.Code[ci.SavedPC].Op != LuaOps.EXTRAARG)
                                throw new Exception();
                            c = fn.Code[ci.SavedPC++].Ax;
                        }
                        int last = ((c - 1) * LFIELDS_PER_FLUSH) + n;
                        for (; n > 0; n--) {
                            table[last--] = _Stack[ra + n];
                        }
                        break;
                    }
                    
                    /* CONSTANTS */
                    case LuaOps.LOADK:
                    {
                        localStack[inst.A] = fn.Constants[inst.Bx];
                        break;
                    }
                    case LuaOps.LOADBOOL:
                    {
                        localStack[inst.A] = new LuaValue(inst.B > 0);
                        if (inst.C != 0) ci.SavedPC++;
                        break;
                    }
                    case LuaOps.LOADNIL:
                    {
                        int b = inst.B;
                        do {
                            setnilvalue(ra++);
                        } while (b-- > 0);
                        break;
                    }
                    
                    /* UPVALUES */
                    case LuaOps.SETTABUP:
                    {
                        var tab = closure.UpValues[inst.A].Value();
                        if (tab.IsNil()) throw new Exception("attempted to index a nil value");
                        var key = RKB(inst, fn, localStack);
                        var val = RKC(inst, fn, localStack);
                        if (tab.Type == LuaType.UserData)
                        {
                            SetUserData(tab,key,val);
                        } 
                        else if (tab.Type == LuaType.Table)
                        {
                            tab.Table().SetValue(key, val, this);
                        }
                        else
                            throw new Exception("cannot index type");
                        CheckStackframe(ref sref, ref localStack);
                        break;
                    }
                    
                    case LuaOps.GETTABUP:
                    {
                        var tab = closure.UpValues[inst.B].Value();
                        if (tab.IsNil()) throw new Exception("attempted to index a nil value");
                        var key = RKC(inst, fn, localStack);
                        if (tab.Type == LuaType.UserData)
                        {
                            localStack[inst.A] = IndexUserData(tab, key, false);
                            CheckStackframe(ref sref, ref localStack);
                        }
                        else
                        {
                            var val = tab.Table()[key];
                            if (val.IsNil())
                            {
                                if (CallBinTM(ref tab, ref key, ra, TMS.INDEX))
                                {
                                    CheckStackframe(ref sref, ref localStack);
                                    break;
                                }
                            }
                            localStack[inst.A] = val;
                        }

                        break;
                    }
                    case LuaOps.GETUPVAL:
                    {
                        localStack[inst.A] = closure.UpValues[inst.B].Value();
                        break;
                    }
                    case LuaOps.SETUPVAL:
                    {
                        closure.UpValues[inst.B].Value() = localStack[inst.A];
                        break;
                    }
                    case LuaOps.CONCAT:
                    {
                        localStack[inst.A] = Concat(localStack, inst.B, inst.C);
                        break;
                    }
                    
                    /* FUNCTIONS */
                    case LuaOps.VARARG:
                    {
                        int b = inst.B - 1;
                        int j;
                        int n = (baseTop - ci.Func) - fn.NumParams - 1;
                        if (b < 0)
                        {
                            b = n; //get all var arguments
                            CheckStack(n);
                            CheckStackframe(ref sref, ref localStack);
                            top = ra + n;
                        }
                        for (j = 0; j < b; j++) {
                            if (j < n) {
                                _Stack[ra + j] = _Stack[baseTop - n + j];
                            }
                            else {
                                setnilvalue(ra + j);
                            }
                        }
                        break;
                    }
                    case LuaOps.TAILCALL:
                    {
                        int b = inst.B;
                        if (b != 0) top = ra + b;
                        LAssert(inst.C - 1 == LUA_MULTRET);
                        if (!luaD_precall(ra, LUA_MULTRET))
                        {
                            var nci = ci;
                            var oci = nci.Previous;
                            var nfunc = nci.Func;
                            var ofunc = oci.Func;
                            var lim = nci.Base + (_Stack[nfunc].Object<LuaFunction>().Prototype).NumParams;
                            int aux;
                            if (fn.Protos.Length > 0) CloseUpVals(oci.Base);
                            /* move new frame into old one */
                            for (aux = 0; nfunc + aux < lim; aux++)
                                _Stack[ofunc + aux] = _Stack[nfunc + aux];
                            oci.Base = ofunc + (nci.Base - nfunc); //fix base
                            oci.Top = top = ofunc + (top - nfunc); //fix top
                            oci.SavedPC = nci.SavedPC;
                            oci.CallStatus |= CIST_TAIL;
                            ci = oci;
                            LAssert(top == oci.Base + _Stack[ofunc].Object<LuaFunction>().Prototype.MaxStackSize);
                            goto newframe;
                        }
                        break;
                    }
                    case LuaOps.CALL:
                    {
                        int b = inst.B;
                        int nresults = inst.C - 1;
                        if (b != 0) { top = ra + b; }
                        var _ci = ci;
                        if (luaD_precall(ra, nresults)) {
                            CheckStackframe(ref sref, ref localStack);
                            if (nresults >= 0) top = _ci.Top;
                            baseTop = _ci.Base;
                            break;
                        }
                        else
                        {
                            ci.CallStatus |= CIST_REENTRY;
                            goto newframe;
                        }
                    }
                    case LuaOps.TFORCALL:
                    {
                        //Do iterator call
                        int cb = ra + 3;
                        _Stack[cb + 2] = _Stack[ra + 2];
                        _Stack[cb + 1] = _Stack[ra + 1];
                        _Stack[cb] = _Stack[ra];
                        top = cb + 3;
                        Call(cb, inst.C, 0);
                        top = ci.Top;
                        //Finish loop
                        inst = fn.Code[ci.SavedPC++];
                        LAssert(inst.Op == LuaOps.TFORLOOP);
                        ra = baseTop + inst.A;
                        goto case LuaOps.TFORLOOP;
                    }
                    case LuaOps.TFORLOOP:
                    {
                        if (!_Stack[ra + 1].IsNil()) {
                            _Stack[ra] = _Stack[ra + 1];
                            ci.SavedPC += inst.sBx;
                        }
                        break;
                    }
                    case LuaOps.RETURN:
                    {
                        int b = inst.B;
                        if (b != 0) top = ra + b - 1;
                        if (fn.Protos.Length > 0) CloseUpVals(baseTop);
                        var _thisCi = ci;
                        b = luaD_poscall(ra);
                        if ((_thisCi.CallStatus & CIST_REENTRY) == 0)
                            return; //Return, external invoke
                        else {
                            //Lua internal invoke
                            if (b != 0) top = ci.Top;
                            goto newframe;
                        }
                    }
                    default:
                        throw new NotImplementedException(inst.Op.ToString());
                }
            }
            throw new Exception("Invalid bytecode - Control left function without return");
        }
    }
}