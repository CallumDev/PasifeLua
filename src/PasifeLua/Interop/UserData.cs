using System;
using System.Collections.Generic;

namespace PasifeLua.Interop
{
    public static class UserData
    {
        private static Dictionary<Type, TypeDescriptor> Descriptors = new Dictionary<Type, TypeDescriptor>();

        private static TypeDescriptor lastDescriptor;
        public static TypeDescriptor GetDescriptor(Type t)
        {
            if (lastDescriptor?.Type == t) return lastDescriptor;
            if (Descriptors.TryGetValue(t, out var d))
                lastDescriptor = d;
            return d;
        }

        public static void RegisterType(TypeDescriptor t)
        {
            Descriptors[t.Type] = t;
        }
    }
}