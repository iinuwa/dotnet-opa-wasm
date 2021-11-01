using System.Linq;
using System.Text.RegularExpressions;

namespace Opa.Wasm.Builtins
{
    public static partial class Builtins
    {
        [OpaBuiltin("regex.split")]
        public static string[] RegexSplit(string pattern, string @string)
        {
            return Regex.Split(@string, pattern);
        }
        [OpaBuiltin("regex.find_n")]
        public static string[] RegexFindN(string pattern, string @string, int number)
        {
			var matches = Regex.Matches(@string, pattern);
			return (number != -1 ? matches.Take(number) : matches)
				.Select(m => m.Value)
				.ToArray();
        }
    }
}
