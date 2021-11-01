using System;

namespace Opa.Wasm.Builtins
{
    [AttributeUsage(AttributeTargets.Method)]
    public class OpaBuiltinAttribute : Attribute
    {
        public readonly string BuiltinName;
        public OpaBuiltinAttribute(string builtinName)
        {
            BuiltinName = builtinName;
        }
    }
}
