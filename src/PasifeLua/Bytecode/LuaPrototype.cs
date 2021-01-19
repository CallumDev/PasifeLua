namespace PasifeLua.Bytecode
{
    public class LuaPrototype
    {
        public int LineDefined;
        public int LastLineDefined;
        public byte NumParams;
        public bool IsVararg;
        public byte MaxStackSize;
            
        public Instruction[] Code;
        public LuaValue[] Constants;
        public LuaPrototype[] Protos;

        public UpvalueDescriptor[] Upvalues;

        public string Source;
        public int[] LineInfo;
        public LocalInfo[] LocalInfos;
        public int UpvalNamesSet;
    }
    
    public struct UpvalueDescriptor
    {
        public int Stack;
        public int Index;
        public string Name;
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
                return $"{Stack} {Index} ;{Name}";
            return $"{Stack} {Index}";
        }
    }

    public struct LocalInfo
    {
        public string Name;
        public int StartPC;
        public int EndPC;
    }
}