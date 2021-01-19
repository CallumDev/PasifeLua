using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PasifeLua.Bytecode;

namespace PasifeLua
{
    class Program
    {
        /*private const string CODE = @"
local tab = { 'a', 'b', 'c' }
for k,v in ipairs(tab) do
    print(k,v)
end
";        */

        static void PrintUsage(string name)
        {
            Console.Error.WriteLine($"usage: {name} [options [script [args]]");
            Console.Error.WriteLine("Available options are:");
            Console.Error.WriteLine(" -e\tstat\texecute string 'stat'");
            Console.Error.WriteLine(" -i\t\tenter interactive mode after executing 'script'");
            Console.Error.WriteLine(" -l\tname\trequire library 'name'");
            Console.Error.WriteLine(" -E\t\tignore environment variables");
            Console.Error.WriteLine(" --\t\tstop handling options");
            Console.Error.WriteLine(" - \t\tstop handling options and execute stdin");
        }
        
        static int Main(string[] args)
        {
            var progname = Environment.GetCommandLineArgs()[0];
            bool handlingOptions = true;
            bool execStdin = false;
            bool doRepl = false;
            string execStatement = null;
            List<string> extraArgs = new List<string>();
            for (int i = 0; i < args.Length; i++) {
                if (args[i][0] == '-' && handlingOptions) {
                    switch (args[i])
                    {
                        case "--":
                            handlingOptions = false;
                            break;
                        case "-":
                            execStdin = true;
                            break;
                        case "-i":
                            doRepl = true;
                            break;
                        case "-v":
                            Console.WriteLine(LuaState.VERSION);
                            return 0;
                        case "-e":
                            if (i + 1 < args.Length) {
                                execStatement = args[i + 1];
                            }
                            break;
                        default:
                            Console.Error.WriteLine($"{progname}: unrecognized option '{args[i]}");
                            PrintUsage(progname);
                            return 1;
                    }
                } else {
                    handlingOptions = false;
                    extraArgs.Add(args[i]);
                }
            }

            var lua = new LuaState();
            if (execStatement != null)
            {
                try
                {
                    var c = lua.CreateClosure(new LuaChunk(execStatement, "stdin"));
                    lua.CallFunction(new LuaValue(c), false);
                }
                catch (Exception e) {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                }
            }
            if (execStdin)
            {
                return ExecScript(lua, Console.OpenStandardInput(), "stdin",
                    extraArgs.Select(x => new LuaValue(x)).ToArray());
            }
            else if (extraArgs.Count > 0)
            {
                if(doRepl) Console.WriteLine(LuaState.VERSION);
                using (var stream = File.OpenRead(extraArgs[0]))
                {
                    var retval = ExecScript(lua, stream, extraArgs[0],
                        extraArgs.Skip(1).Select(x => new LuaValue(x)).ToArray());
                    if (!doRepl) return retval;
                    REPL(lua);
                    return 0;
                }
            }
            Console.WriteLine(LuaState.VERSION);
            REPL(lua);
            return 0;
        }

        static int ExecScript(LuaState state, Stream stream, string name, LuaValue[] args)
        {
            try
            {
                var c = state.CreateClosure(new LuaChunk(stream, name));
                
                state.CallFunction(new LuaValue(c), false);
                return 0;
            }
            catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
                return 1;
            }
        }

        static LuaChunk GetChunk(string sofar)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sofar)) return null;
                return new LuaChunk(sofar, "stdin");
            }
            catch (Exception e)
            {
                if (e.Message.Contains("<eof>")) {
                    Console.Write(">> ");
                    return GetChunk(sofar + "\n" + Console.ReadLine());
                }
                else {
                    Console.Error.WriteLine(e.Message);
                    return null;
                }
            }
        }
        static void REPL(LuaState state)
        {
            while (true) {
                Console.Write("> ");
                var chnk = GetChunk(Console.ReadLine());
                if (chnk == null) continue;
                try
                {
                    var c = state.CreateClosure(chnk);
                    state.CallFunction(new LuaValue(c), false);
                }
                catch (Exception e) {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.InnerException?.StackTrace);
                }
            }
        }
        
        
    }
}