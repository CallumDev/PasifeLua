using System;
using System.IO;
using PasifeLua.Interop;
using Xunit;

namespace PasifeLua.Tests
{
    public class DemoClass
    {
        public string SetString;
    }

    public class DemoClassDescriptor : TypeDescriptor
    {
        public DemoClassDescriptor() : base(typeof(DemoClass))
        {
            AddSetter("SetString", new SetString_Set());
            AddGetter("SetString", new SetString_Get());
        }
        
        class SetString_Set : ClrSetterGeneric<DemoClass>
        {
            protected override void Set(DemoClass self, LuaValue value)
            {
                if (!value.AsString(out string v))
                    throw new InvalidCastException();
                self.SetString = v;
            }
        }
        class SetString_Get : ClrGetterGeneric<DemoClass>
        {
            protected override LuaValue Get(DemoClass self)
            {
                return new LuaValue(self.SetString);
            }
        }
    }
    public class StaticInteropTests
    {
        static StaticInteropTests()
        {
            UserData.RegisterType(new DemoClassDescriptor());
        }
        
        [Fact]
        public void SetField_String()
        {
            var dc = new DemoClass();
            var state = new LuaState();
            state.Globals["inst"] = new LuaValue(LuaType.UserData, dc);
            state.DoString("inst.SetString = 'hello'");
            Assert.Equal("hello", dc.SetString);
        }

        [Fact]
        public void GetField_String()
        {
            var dc = new DemoClass() {SetString = "hello"};
            var state = new LuaState();
            var sw = new StringWriter();
            state.StandardOut = sw;
            state.Globals["inst"] = new LuaValue(LuaType.UserData, dc);
            state.DoString("print(inst.SetString)");
            Assert.Equal("hello", sw.ToString().Trim());
        }
    }
}