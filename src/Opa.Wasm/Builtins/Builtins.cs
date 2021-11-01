using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Opa.Wasm.Builtins
{
    public static partial class Builtins
    {
        private static readonly Dictionary<string, MethodInfo> _methods = typeof(Builtins)
                    .GetMethods()
                    .Where(method => method.GetCustomAttribute<OpaBuiltinAttribute>() != null)
                    .ToDictionary(methodInfo => methodInfo.GetCustomAttribute<OpaBuiltinAttribute>().BuiltinName, methodInfo => methodInfo);
        public static MethodBase Lookup(string builtinName)
        {
            return _methods.TryGetValue(builtinName, out MethodInfo method)
				? method
				: throw new InvalidOperationException($"OPA builtin `{builtinName}` is not supported");
        }
    }
}
