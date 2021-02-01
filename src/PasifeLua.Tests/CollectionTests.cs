using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PasifeLua.Tests
{
    public class CollectionTests
    {
        static string RunWithObject(string str, object o)
        {
            var state = new LuaState();
            var sw = new StringWriter();
            state.StandardOut = sw;
            state.Globals["obj"] = new LuaValue(LuaType.UserData, o);
            state.DoString(str);
            return sw.ToString().Trim();
        }
        
        [Fact]
        public void IPairs_StringArray()
        {
            string[] arr = {"Hello", "World"};
            Assert.Equal("1\tHello\n2\tWorld", RunWithObject(@"
            for k, v in ipairs(obj) do
                print(k, v)
            end", arr));   
        }

        [Fact]
        public void IPairs_StringList()
        {
            List<string> arr = new List<string>(new[] {"Hello", "World"});
            Assert.Equal("1\tHello\n2\tWorld", RunWithObject(@"
            for k, v in ipairs(obj) do
                print(k, v)
            end", arr));   
        }
        
        [Fact]
        public void Pairs_StringDictionary()
        {
            var dict = new Dictionary<string, string>()
            {
                {"a", "b"},
                {"c", "d"}
            };
            var res = RunWithObject(@"
            for k, v in pairs(obj) do
                print(k, v)
            end
", dict);
            Assert.Equal("a\tb\nc\td", res);
        }
        
        [Fact]
        public void Pairs_StringArray()
        {
            string[] arr = {"Hello", "World"};
            Assert.Equal("1\tHello\n2\tWorld", RunWithObject(@"
            for k, v in pairs(obj) do
                print(k, v)
            end", arr));   
        }

        [Fact]
        public void ArrayLength()
        {
            string[] arr = {"Hello", "World", "Three"};
            Assert.Equal("3", RunWithObject("print (#obj)", arr));
        }

        [Fact]
        public void ListLength()
        {
            List<float> list = new List<float>(new[] {1f, 2f, 3f});
            Assert.Equal("3", RunWithObject("print (#obj)", list));
        }

        [Fact]
        public void DictionaryLength()
        {
            var dict = new Dictionary<string, string>()
            {
                {"a", "b"},
                {"c", "d"}
            };
            Assert.Equal("2", RunWithObject("print (#obj)", dict));
        }

        [Fact]
        public void DictionaryIndex()
        {
            static void RunTest(string expected, object o)
            {
                Assert.Equal(expected, RunWithObject(
                    @"print (type(obj['a']))
                     print(obj['a'])", 
                    o)
                );
            }
            var number = new Dictionary<string, double>(){{"a", 3f}};
            var str = new Dictionary<string, string>() {{"a", "a"}};
           
            RunTest("number\n3", number);
            RunTest("string\na", str);
        }
        
        [Fact]
        public void DictionarySet()
        {
            var dict = new Dictionary<string, string>();
            dict.Add("a","A");
            var res = RunWithObject(@"
                print(obj['a'])
                obj['b'] = 'B'
                print(obj['b'])
            ", dict);
            Assert.Equal("A\nB", res);
        }

        [Fact]
        public void ArrayIndex()
        {
            string[] array = {"a", "b", "c"};
            Assert.Equal("b", RunWithObject("print (obj[2])", array));
        }

        [Fact]
        public void ArraySet()
        {
            string[] array = {"a", "b", "c"};
            var res = RunWithObject(@"
            obj[2] = 'B'
            print(obj[1])
            print(obj[2])
", array);
            Assert.Equal("a\nB", res);
        }

        [Fact]
        public void LoopArrayNumeric()
        {
            string[] array = {"a", "b", "c"};
            var res = RunWithObject(@"
            for i = 1, #obj do
                print(obj[i])
            end
", array);
            Assert.Equal("a\nb\nc", res);
        }

        [Fact]
        public void ListAddString()
        {
            var list = new List<string>();
            list.Add("a");
            var res = RunWithObject(@"
                obj:Add('b')
                print(obj[2])
                print(#obj)
            ", list);
            Assert.Equal("b\n2", res);
        }
    }
}