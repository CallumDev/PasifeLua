using System;
using System.Collections.Generic;
using PasifeLua.Bytecode;

namespace PasifeLua.luac
{
    //LuaPrototype using RefVector<T>
    //Reduces to LuaPrototype with Arrays to reduce final memory usage
    class CompilerPrototype
    {
        public int LineDefined;
        public int LastLineDefined;
        public byte NumParams;
        public bool IsVararg;
        public byte MaxStackSize;
            
        public RefList<Instruction> Code = new RefList<Instruction>(true);
        public RefList<LuaValue> Constants = new RefList<LuaValue>();
        public RefList<CompilerPrototype> Protos = new RefList<CompilerPrototype>();

        public RefList<UpvalueDescriptor> Upvalues = new RefList<UpvalueDescriptor>();

        public string Source;
        public RefList<int> LineInfo = new RefList<int>();
        public RefList<LocalInfo> LocalInfos = new RefList<LocalInfo>();

        public LuaPrototype ToProto()
        {
            LuaPrototype[] _protos = new LuaPrototype[Protos.Count];
            for (int i = 0; i < Protos.Count; i++)
                _protos[i] = Protos[i].ToProto();
            
            return new LuaPrototype()
            {
                LineDefined = LineDefined,
                LastLineDefined = LastLineDefined,
                NumParams = NumParams,
                IsVararg = IsVararg,
                MaxStackSize = MaxStackSize,
                Code = Code.ToArray(),
                Constants = Constants.ToArray(),
                Protos = _protos,
                Upvalues = Upvalues.ToArray(),
                Source = Source,
                LineInfo = LineInfo.ToArray(),
                LocalInfos = LocalInfos.ToArray(),
                UpvalNamesSet = Upvalues.Count
            };
        }
    }
}