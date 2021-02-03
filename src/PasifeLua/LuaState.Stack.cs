using System;

namespace PasifeLua
{
    public partial class LuaState
    {
        private const int MINSTACK = 20;
        private const int BASIC_STACK_SIZE = 2 * MINSTACK;
        private const int EXTRA_STACK = 5;
        private const int LUAI_MAXSTACK = 1000000;
        
        internal LuaValue[] _Stack;
        private int top;
        private int stack_last;

        public int Top => top;
        void StackInit()
        {
            _Stack = new LuaValue[BASIC_STACK_SIZE];
            top = 0;
            stack_last = _Stack.Length - EXTRA_STACK;
            ci = new CallInfo();
            ci.Func = top;
            setnilvalue(top++);
            ci.Top = top + MINSTACK;
        }

        
        public int GetTop()
        {
            return (top - (ci.Func + 1));
        }

        public bool CheckStack(int size)
        {
            if (stack_last - top > size) {
                return true;
            }
            else
            {
                int inuse = top + EXTRA_STACK;
                if (inuse > LUAI_MAXSTACK - size)
                    return false;
                else
                {
                    int needed = top + size + EXTRA_STACK;
                    int newsize = 2 * size;
                    if (newsize > LUAI_MAXSTACK) newsize = LUAI_MAXSTACK;
                    if (newsize < needed) newsize = needed;
                    if (newsize > LUAI_MAXSTACK) {
                        throw new Exception("Lua Stack Overflow");
                    }
                    else {
                        Array.Resize(ref _Stack, newsize);
                        stack_last = newsize - EXTRA_STACK;
                        return true;
                    }
                }
            }
        }
        
        public void Insert(int idx)
        {
            int p = idx > 0 ? (ci.Func + idx) : top + idx;
            int q = top;
            for (q = top; q > p; q--) {
                _Stack[q] = _Stack[q - 1];
            }
            _Stack[p] = _Stack[top];
        }
        
        public void Replace(int idx)
        {
            api_checknelems(1);
            Value(idx) = _Stack[top - 1];
            top--;
        }

        ref LuaValue index2addr(int idx)
        {
            if (idx > 0)
            {
                api_check(idx <= ci.Top - (ci.Func + 1), "unacceptable index");
                return ref _Stack[ci.Func + idx];
            }
            else if (idx == LUA_REGISTRYINDEX)
            {
                return ref luaRegistry;
            }
            else {
                api_check(idx != 0 && -idx <= top - (ci.Func + 1), "invalid index");
                return ref _Stack[top + idx];
            }
        }

        public ref LuaValue Value(int idx) => ref index2addr(idx);

        public void Push(LuaFunction closure)
        {
            _Stack[top] = new LuaValue(closure);
            api_incr_top();
        }

        public void Push(LuaValue value)
        {
            _Stack[top] = value;
            api_incr_top();
        }

        public void Push(string str)
        {
            _Stack[top] = new LuaValue(str);
            api_incr_top();
        }

        public void Push(double number)
        {
            _Stack[top] = new LuaValue(LuaType.Number, number);
            api_incr_top();
        }

        public void Pop(int n)
        {
            SetTop(-(n) - 1);
        }
        
        public void SetTop(int idx)
        {
            int func = ci.Func;
            if (idx >= 0) {
                api_check(idx <= stack_last - (func + 1), "new top too large");
                while (top < (func + 1) + idx)
                    setnilvalue(top++);
                top = (func + 1) + idx;
            }
            else
            {
                api_check(-(idx+1) <= (top - (func + 1)), "invalid new top");
                top += idx + 1;
            }
        }
        void setnilvalue(int index)
        {
            _Stack[index] = new LuaValue(LuaType.Nil);
        }
    }
}