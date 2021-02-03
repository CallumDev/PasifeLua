using System;
using System.IO;
using System.Text;
using PasifeLua.Interop;
using PasifeLua.Libs;

namespace PasifeLua
{
    class IOFileDescriptor : TypeDescriptor
    {
        public IOFileDescriptor() : base(typeof(LuaIOFile))
        {
            AddFunction("write", new DelegateClrFunction((L) =>
            {
                var file = L.Value(1).Object<LuaIOFile>();
                var top = L.GetTop();
                for (int n = 2; n <= top; n++) {
                    file.Write(L.Value(n).ToString(L));
                }
                file.Flush();
                return 0;
            }));
            AddFunction("close", new DelegateClrFunction((L) =>
            {
                var file = L.Value(1).Object<LuaIOFile>();
                file.Close();
                return 0;
            }));
            AddFunction("read", new DelegateClrFunction((L) =>
            {
                var file = L.Value(1).Object<LuaIOFile>();
                string format = "*l";
                if (L.GetTop() > 1)
                    format = LibUtils.GetString(L, 2, "read");
                L.Push(file.Read(format));
                return 1;
            }));
        }
    }
    public class LuaIOFile
    {
        static LuaIOFile()
        {
            UserData.RegisterType(new IOFileDescriptor());
        }
        private TextWriter writer;
        private TextReader reader;
        private string info = "";
        private bool closed = false;
        public bool Closed => closed;

        public virtual TextWriter Writer => writer;
        public virtual TextReader Reader => reader;

        protected LuaIOFile(string info)
        {
            this.info = info;
        }
        public LuaIOFile(TextWriter w, string info = null)
        {
            writer = w;
            this.info = info;
        }

        public LuaIOFile(TextReader r, string info = null)
        {
            reader = r;
            this.info = info;
        }

        internal LuaValue Read(string fmt)
        {
            if (fmt == "*n")
            {
                throw new NotImplementedException();
            } 
            else if (fmt == "*a")
            {
                return new LuaValue(reader.ReadToEnd());
            }
            return new LuaValue(reader.ReadLine());
        }

        internal void Write(string str)
        {
            Writer.Write(str);
        }

        internal virtual void Close()
        {
            closed = true;
            if (Writer != null) Writer.Close();
            if (Reader != null) Reader.Close();
        }

        internal void Flush() => Writer.Flush();
        
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(info))
                return $"file ({info})";
            else
                return $"file";
        }
    }

    class LuaStandardIOFile : LuaIOFile
    {
        private Func<TextWriter> writer;
        private Func<TextReader> reader;
        public override TextWriter Writer => writer?.Invoke();
        public override TextReader Reader => reader?.Invoke();

        public LuaStandardIOFile(Func<TextWriter> w, Func<TextReader> r, string info) : base(info)
        {
            writer = w;
            reader = r;
        }

        internal override void Close()
        {
            //can't close stdout or stdin
        }
    }
    
}