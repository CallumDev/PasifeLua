using PasifeLua.Bytecode;

namespace PasifeLua
{
    public class LuaFunction
    {
        public LuaPrototype Prototype;
        public UpVal[] UpValues;
        internal LuaState state;

        internal LuaFunction(LuaState state, LuaPrototype p, int n)
        {
            this.state = state;
            Prototype = p;
            UpValues = new UpVal[n];
        }
    }
}