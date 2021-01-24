using System;
using System.Collections.Generic;
using System.Threading;

namespace PasifeLua.Interop
{
    public static class UserData
    {
        private static Dictionary<Type, TypeDescriptor> Descriptors = new Dictionary<Type, TypeDescriptor>();

        private static ThreadLocal<TypeDescriptor> lastDescriptor = new ThreadLocal<TypeDescriptor>();
        public static TypeDescriptor GetDescriptor(Type t)
        {
            if (lastDescriptor.Value?.Type == t) return lastDescriptor.Value;
            TypeDescriptor d;
            lock (Descriptors) {
                if (Descriptors.TryGetValue(t, out d))
                    lastDescriptor.Value = d;
            }
            return d;
        }

        public static void RegisterType(TypeDescriptor t)
        {
            lock (Descriptors)
            {
                Descriptors[t.Type] = t;
            }
        }
    }
}