using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PasifeLua.Tests
{
    public class LuaCodeTestData : IEnumerable<object[]>
    {

        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (var s in Directory.GetFiles("Tests", "*.lua"))
                yield return new object[] { Path.GetFileName(s) };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class LuaCodeTests
    {
        static string RunTestFile(string file)
        {
            var writer = new StringWriter();
            var state = new LuaState() {StandardOut = writer};
            state.DoString("arg = {}");
            state.DoString("package.path = './Tests/Modules/?.lua'");
            var txt = File.ReadAllText(file);
            if (txt.StartsWith("#!")) txt = txt.Substring(txt.IndexOf('\n'));
            state.DoString(txt, Path.GetFileName(file));
            return writer.ToString().Trim();
        }
        
        [Theory]
        [ClassData(typeof(LuaCodeTestData))]
        public void LuaUnitTests(string filename)
        {
            var filepath = Path.Combine("Tests", filename);
            var expectedPath = Path.Combine("Tests", "expected", filename) + ".txt";
            Assert.Equal(File.ReadAllText(expectedPath).Trim(), RunTestFile(filepath));
        }
    }
}