using System;
using System.IO;
using System.Text;

namespace PasifeLua.Bytecode
{ 
    public class LuaChunk
    {
        public LuaPrototype Prototype;
            
        private static readonly byte[] HEADER = 
        {
            0x1b, 0x4c, 0x75, 0x61, //LUA_SIGNATURE
            0x52, 0x00, //Lua 5.2
            //Bytecode arguments
            0x01,
            0x04, //4-byte vm instructions
            0x08, //size_t is 8 bytes 
            0x04, //instruction is 4 bytes
            0x08, //double is 8 bytes
            0x00, //is float
            //conversion data
            0x19, 0x93, 0x0d, 0x0a, 0x1a, 0x0a
        };
        
        static int ValidateHeader(byte[] bytecode)
        {
            for (int i = 0; i < HEADER.Length; i++)
            {
                if (bytecode[i] != HEADER[i]) return i;
            }
            return -1;
        }


        public LuaChunk(byte[] bytecode)
        {
            SetFromByteCode(bytecode);
        }

        public LuaChunk(string code, string source)
        {
            SetFromTextReader(new StringReader(code), source);
        }

        public LuaChunk(Stream stream, string source)
        {
            byte[] arr;
            using (var s2 = new MemoryStream()) {
                stream.CopyTo(s2);
                arr = s2.ToArray();
            }
            if (arr[0] == 0x1b) {
               SetFromByteCode(arr);
            }else {
                using (var reader = new StreamReader(new MemoryStream(arr, false)))
                {
                    SetFromTextReader(reader, source);
                }
            }
        }
        void SetFromByteCode(byte[] bytecode)
        {
            int errOffset;
            if ((errOffset = ValidateHeader(bytecode)) != -1) {
                throw new Exception($"Header mismatch at byte 0x{errOffset:X}");
            }
            int index = HEADER.Length;
            Prototype = LoadFunction(bytecode, ref index);
        }

        void SetFromTextReader(TextReader tr, string source)
        {
            Prototype = luac.Parser.Compile(tr, source, tr.Read());
        }
        
        

        LuaPrototype LoadFunction(byte[] bytecode, ref int index)
        {
            var func = new LuaPrototype();
            func.LineDefined = LoadInt(bytecode, ref index);
            func.LastLineDefined = LoadInt(bytecode, ref index);
            func.NumParams = LoadByte(bytecode, ref index);
            func.IsVararg = LoadBool(bytecode, ref index);
            func.MaxStackSize = LoadByte(bytecode, ref index);
            func.Code = new Instruction[LoadInt(bytecode, ref index)];
            LoadInstructions(bytecode, func.Code, ref index);
            func.Constants = new LuaValue[LoadInt(bytecode, ref index)];
            for (int i = 0; i < func.Constants.Length; i++)
                func.Constants[i] = LoadConstant(bytecode, ref index);
            //Nested functions
            func.Protos = new LuaPrototype[LoadInt(bytecode, ref index)];
            for (int i = 0; i < func.Protos.Length; i++)
                func.Protos[i] = LoadFunction(bytecode, ref index);
            //Upvalues
            func.Upvalues = new UpvalueDescriptor[LoadInt(bytecode, ref index)];
            for (int i = 0; i < func.Upvalues.Length; i++)
            {
                func.Upvalues[i] = new UpvalueDescriptor() {
                    Stack = LoadByte(bytecode, ref index),
                    Index = LoadByte(bytecode, ref index)
                };
            }
            //Debug
            func.Source = LoadString(bytecode, ref index);
            func.LineInfo = new int[LoadInt(bytecode, ref index)];
            for (int i = 0; i < func.LineInfo.Length; i++)
                func.LineInfo[i] = LoadInt(bytecode, ref index);
            func.LocalInfos = new LocalInfo[LoadInt(bytecode, ref index)];
            for (int i = 0; i < func.LocalInfos.Length; i++)
            {
                func.LocalInfos[i] = new LocalInfo()
                {
                    Name = LoadString(bytecode, ref index),
                    StartPC = LoadInt(bytecode, ref index),
                    EndPC = LoadInt(bytecode, ref index)
                };
            }
            int upvalnames = LoadInt(bytecode, ref index);
            func.UpvalNamesSet = upvalnames;
            for (int i = 0; i < upvalnames; i++)
                func.Upvalues[i].Name = LoadString(bytecode, ref index);
            return func;
        }

        public byte[] Dump()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write(HEADER);
                WriteFunction(writer, Prototype);
                return stream.ToArray();
            }
        }

        static void WriteFunction(BinaryWriter writer, LuaPrototype proto)
        {
            writer.Write((int) proto.LineDefined);
            writer.Write((int) proto.LastLineDefined);
            writer.Write((byte) proto.NumParams);
            writer.Write((byte) (proto.IsVararg ? 1 : 0));
            writer.Write((byte) proto.MaxStackSize);
            writer.Write((int)proto.Code.Length);
            for(int i = 0; i < proto.Code.Length; i++)
                writer.Write(proto.Code[i].Encoded);
            writer.Write((int) proto.Constants.Length);
            foreach (var c in proto.Constants) {
                WriteConstant(writer, c);
            }
            writer.Write((int) proto.Protos.Length);
            foreach (var p in proto.Protos) {
                WriteFunction(writer, p);
            }
            writer.Write((int) proto.Upvalues.Length);
            foreach (var u in proto.Upvalues)
            {
                writer.Write((byte) u.Stack);
                writer.Write((byte) u.Index);
            }
            WriteString(writer, proto.Source);
            writer.Write(proto.LineInfo.Length);
            foreach (var l in proto.LineInfo) writer.Write(l);
            writer.Write(proto.LocalInfos.Length);
            foreach (var l in proto.LocalInfos)
            {
                WriteString(writer, l.Name);
                writer.Write(l.StartPC);
                writer.Write(l.EndPC);
            }
            writer.Write(proto.UpvalNamesSet);
            for (int i = 0; i < proto.UpvalNamesSet; i++) {
                WriteString(writer, proto.Upvalues[i].Name);
            }
        }

        static void WriteConstant(BinaryWriter writer, LuaValue constant)
        {
            writer.Write((byte) constant.Type);
            switch (constant.Type)
            {
                case LuaType.Nil:
                    break;
                case LuaType.Boolean:
                    writer.Write((byte) (constant.number > 0 ? 1 : 0));
                    break;
                case LuaType.Number:
                    writer.Write(constant.number);
                    break;
                case LuaType.String:
                    WriteString(writer, (string) constant.obj);
                    break;
                default:
                    throw new Exception($"Bad constant type {constant.Type}");
            }
        }

        static void WriteString(BinaryWriter writer, string s)
        {
            if (s == null)
            {
                writer.Write((long) 0);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                writer.Write((long) (bytes.Length + 1));
                writer.Write(bytes);
                writer.Write((byte) 0);
            }
        }
        
        static LuaValue LoadConstant(byte[] bytecode, ref int index)
        {
            var type = (LuaType) LoadByte(bytecode, ref index);
            switch (type)
            {
                case LuaType.Nil:
                    return new LuaValue(LuaType.Nil);
                case LuaType.Boolean:
                    return new LuaValue(LoadByte(bytecode, ref index) != 0);
                case LuaType.Number:
                    var number = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytecode, index));
                    index += 8;
                    return new LuaValue(LuaType.Number, number);
                case LuaType.String:
                    return new LuaValue(LuaType.String, LoadString(bytecode, ref index));
                default:
                    throw new Exception($"Bad constant type {type}");
            }
        }

        static string LoadString(byte[] bytecode, ref int index)
        {
            var len = LoadLong(bytecode, ref index);
            if (len == 0) return null;
            var bytes = new byte[len];
            for (int i = 0; i < len; i++)
                bytes[i] = bytecode[index++];
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        static int LoadInt(byte[] bytecode, ref int index)
        {
            var i = BitConverter.ToInt32(bytecode, index);
            index += 4;
            return i;
        }

        static long LoadLong(byte[] bytecode, ref int index)
        {
            var i = BitConverter.ToInt64(bytecode, index);
            index += 8;
            return i;
        }

        static unsafe void LoadInstructions(byte[] bytecode, Instruction[] dest, ref int index)
        {
            fixed (byte* bc = &bytecode[index])
            {
                var i = (Instruction*) bc;
                for (int j = 0; j < dest.Length; j++)
                {
                    dest[j] = i[j];
                }
                index += (dest.Length * 4);
            }
        }

        static bool LoadBool(byte[] bytecode, ref int index)
        {
            return bytecode[index++] != 0;
        }

        static byte LoadByte(byte[] bytecode, ref int index)
        {
            return bytecode[index++];
        }
    }
}