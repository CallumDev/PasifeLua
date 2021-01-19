namespace PasifeLua
{
    public class UpVal
    {
        public int StackPtr;
        public LuaState State;
        public LuaValue ValueStore;
        public void Close()
        {
            if (State != null) {
                ValueStore = State._Stack[StackPtr];
                State = null;
            }    
        }
        public ref LuaValue Value()
        {
            if (State != null)
                return ref State._Stack[StackPtr];
            return ref ValueStore;
        }
    }
}