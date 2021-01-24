using System;

namespace PasifeLua.Interop
{
    public abstract class FunctionOverload : ClrFunction
    {
        public Type[] Parameters { get; }
        protected FunctionOverload(params Type[] parameters) {
            Parameters = parameters;
        }
    }
}