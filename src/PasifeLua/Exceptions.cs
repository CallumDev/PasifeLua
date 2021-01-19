using System;
namespace PasifeLua
{
    public class LuaCompilerErrorException : Exception
    {
        public LuaCompilerErrorException(string message) : base(message) { }
    }
    
    public class LuaRuntimeException : Exception
    {
        public LuaRuntimeException(string message, Exception innerException) : base(message, innerException) { }
    }
}