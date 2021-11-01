using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Opa.Wasm.Builtins
{
    public partial class Builtins
    {
		// Modified from https://www.codeproject.com/Articles/19274/A-printf-implementation-in-C
        [OpaBuiltin("sprintf")]
        public static string Sprintf( string Format, params object[] Parameters )
        {
            #region Variables
            StringBuilder f = new StringBuilder();
            Regex r = new Regex( @"\%(\d*\$)?([\'\#\-\+ ]*)(\d*)(?:\.(\d+))?([hl])?([dioxXucsfeEgGpn%])" );
            int defaultParamIx = 0;
            int paramIx;
            #endregion

            // find all format parameters in format string
            f.Append( Format );
            //"%[parameter][flags][width][.precision][length]type"
            Match m = r.Match(f.ToString());
            while ( m.Success )
            {
                #region parameter index
                paramIx = defaultParamIx;
                if ( m.Groups[1] != null && m.Groups[1].Value.Length > 0 )
                {
                    string val = m.Groups[1].Value[0..^1];
                    paramIx = Convert.ToInt32( val ) - 1;
                };
                #endregion

                #region format flags
                // extract format flags
                bool flagAlternate = false;
                bool flagLeft2Right = false;
                bool flagPositiveSign = false;
                bool flagPositiveSpace = false;
                bool flagZeroPadding = false;
                bool flagGroupThousands = false;
                if ( m.Groups[2] != null && m.Groups[2].Value.Length > 0 )
                {
                    string flags = m.Groups[2].Value;

                    flagAlternate =  flags.IndexOf( '#' ) >= 0 ;
                    flagLeft2Right =  flags.IndexOf( '-' ) >= 0 ;
                    flagPositiveSign =  flags.IndexOf( '+' ) >= 0 ;
                    flagPositiveSpace =  flags.IndexOf( ' ' ) >= 0 ;
                    flagGroupThousands =  flags.IndexOf( '\'' ) >= 0 ;

                    // positive + indicator overrides a
                    // positive space character
                    if ( flagPositiveSign && flagPositiveSpace )
                        flagPositiveSpace = false;
                }
                #endregion

                #region field length
                // extract field length and
                // pading character
                char paddingCharacter = ' ';
                int fieldLength = int.MinValue;
                if ( m.Groups[3] != null && m.Groups[3].Value.Length > 0 )
                {
                    fieldLength = Convert.ToInt32( m.Groups[3].Value );
                    flagZeroPadding = ( m.Groups[3].Value[0] == '0' );
                }
                #endregion

                if ( flagZeroPadding )
                    paddingCharacter = '0';

                // left2right allignment overrides zero padding
                if ( flagLeft2Right && flagZeroPadding )
                {
                    paddingCharacter = ' ';
                }

                #region field precision
                // extract field precision
                int fieldPrecision = int.MinValue;
                if ( m.Groups[4] != null && m.Groups[4].Value.Length > 0 )
                    fieldPrecision = Convert.ToInt32( m.Groups[4].Value );
                #endregion

                #region short / long indicator
                // extract short / long indicator
                char shortLongIndicator = Char.MinValue;
                if ( m.Groups[5] != null && m.Groups[5].Value.Length > 0 )
                    shortLongIndicator = m.Groups[5].Value[0];
                #endregion

                #region format specifier
                // extract format
                char formatSpecifier = Char.MinValue;
                if ( m.Groups[6] != null && m.Groups[6].Value.Length > 0 )
                    formatSpecifier = m.Groups[6].Value[0];
                #endregion

                // default precision is 6 digits if none is specified except
                if ( fieldPrecision == int.MinValue &&
                    formatSpecifier != 's' &&
                    formatSpecifier != 'c' &&
                    Char.ToUpper( formatSpecifier ) != 'X' &&
                    formatSpecifier != 'o' )
                    fieldPrecision = 6;

                object o;
                #region get next value parameter
                // get next value parameter and convert value parameter depending on short / long indicator
                if (Parameters == null || paramIx >= Parameters.Length)
                    o = null;
                else
                {
                    o = Parameters[paramIx];

                    if (shortLongIndicator == 'h')
                    {
                        o = o switch {
                            int i => (short)i,
                            long l => (short)l,
                            uint ui => (ushort)ui,
                            ulong ul => (ushort)ul,
							_ => throw new ArgumentException($"Cannot format {o.GetType()} using %{shortLongIndicator}"),
                        };
                    }
                    else if (shortLongIndicator == 'l')
                    {
                        o = o switch {
                            short s => (long)s,
                            int i => (long)i,
                            ushort u => (ulong)u,
                            uint ui => (ulong)ui,
							_ => throw new ArgumentException($"Cannot format {o.GetType()} using %{shortLongIndicator}"),
                        };
                    }
                }
                #endregion

                // convert value parameters to a string depending on the formatSpecifier
                string w = string.Empty;
                switch ( formatSpecifier )
                {
                    #region % - character
                    case '%':   // % character
                        w = "%";
                        break;
                    #endregion
                    #region d - integer
                    case 'd':   // integer
                        w = FormatNumber(  flagGroupThousands ? "n" : "d" ,
                                        fieldLength, int.MinValue, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region i - integer
                    case 'i':   // integer
                        goto case 'd';
                    #endregion
                    #region o - octal integer
                    case 'o':   // octal integer - no leading zero
                        w = FormatOct(flagAlternate,
                                        fieldLength, flagLeft2Right,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region x - hex integer
                    case 'x':   // hex integer - no leading zero
                        w = FormatHex( "x", flagAlternate,
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region X - hex integer
                    case 'X':   // same as x but with capital hex characters
                        w = FormatHex( "X", flagAlternate,
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region u - unsigned integer
                    case 'u':   // unsigned integer
                        w = FormatNumber( ( flagGroupThousands ? "n" : "d" ),
                                        fieldLength, int.MinValue, flagLeft2Right,
                                        false, false,
                                        paddingCharacter, ToUnsigned( o ) );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region c - character
                    case 'c':   // character
                        if ( IsNumericType( o ) )
                            w = Convert.ToChar( o ).ToString();
                        else if ( o is char c)
                            w = c.ToString();
                        else if ( o is string s && s.Length > 0 )
                            w = s[0].ToString();
                        defaultParamIx++;
                        break;
                    #endregion
                    #region s - string
                    case 's':   // string
                        string t = "{0" + ( fieldLength != int.MinValue ? "," + ( flagLeft2Right ? "-" : string.Empty ) + fieldLength.ToString() : String.Empty ) + ":s}";
                        w = o.ToString();
                        if ( fieldPrecision >= 0 )
                            w = w[..fieldPrecision];

                        if ( fieldLength != int.MinValue )
                            if ( flagLeft2Right )
                                w = w.PadRight( fieldLength, paddingCharacter );
                            else
                                w = w.PadLeft( fieldLength, paddingCharacter );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region f - double number
                    case 'f':   // double
                        w = FormatNumber( ( flagGroupThousands ? "n" : "f" ),
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region e - exponent number
                    case 'e':   // double / exponent
                        w = FormatNumber( "e",
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region E - exponent number
                    case 'E':   // double / exponent
                        w = FormatNumber( "E",
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region g - general number
                    case 'g':   // double / exponent
                        w = FormatNumber( "g",
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region G - general number
                    case 'G':   // double / exponent
                        w = FormatNumber( "G",
                                        fieldLength, fieldPrecision, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, o );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region p - pointer
                    case 'p':   // pointer
                        if ( o is IntPtr ptr)
                            w = "0x" + ptr.ToString( "x" );
                        defaultParamIx++;
                        break;
                    #endregion
                    #region n - number of processed chars so far
                    case 'n':   // number of characters so far
                        w = FormatNumber( "d",
                                        fieldLength, int.MinValue, flagLeft2Right,
                                        flagPositiveSign, flagPositiveSpace,
                                        paddingCharacter, m.Index );
                        break;
                    #endregion
                    default:
                        w = string.Empty;
                        defaultParamIx++;
                        break;
                }

                // replace format parameter with parameter value
                // and start searching for the next format parameter
                // AFTER the position of the current inserted value
                // to prohibit recursive matches if the value also
                // includes a format specifier
                f.Remove( m.Index, m.Length );
                f.Insert( m.Index, w );
                m = r.Match( f.ToString(), m.Index + w.Length );
            }

            return f.ToString();
        }
        private static string FormatNumber( string NativeFormat, int FieldLength,
                                            int FieldPrecision, bool Left2Right,
                                            bool PositiveSign, bool PositiveSpace,
                                            char Padding, object Value )
        {
            string w = string.Empty;
            string lengthFormat = "{0" + ( FieldLength != int.MinValue ?
                                            "," + ( Left2Right ?
                                                    "-" :
                                                    string.Empty ) + FieldLength.ToString() :
                                            string.Empty ) + "}";
            string numberFormat = "{0:" + NativeFormat + ( FieldPrecision != int.MinValue ?
                                            FieldPrecision.ToString() :
                                            "0" ) + "}";

            if ( IsNumericType( Value ) )
            {
                w = string.Format( numberFormat, Value );

                if ( Left2Right || Padding == ' ' )
                {
                    if ( IsPositive( Value, true ) )
                        w = ( PositiveSign ?
                                "+" : ( PositiveSpace ? " " : string.Empty ) ) + w;
                    w = string.Format( lengthFormat, w );
                }
                else
                {
                    if ( w.StartsWith( "-" ) )
                        w = w[1..];
                    if ( FieldLength != int.MinValue )
                        w = w.PadLeft( FieldLength - 1, Padding );
                    if ( IsPositive( Value, true ) )
                        w = ( PositiveSign ?
                                "+" : ( PositiveSpace ?
                                        " " : ( FieldLength != int.MinValue ?
                                                Padding.ToString() : string.Empty ) ) ) + w;
                    else
                        w = "-" + w;
                }
            }

            return w;
        }

        public static long UnboxToLong( object Value, bool Round )
        {
            return Value switch
            {
                sbyte sb => (long)sb,
                short i16 => (long)i16,
                int i => (long)i,
                long l => (long)l,
                byte b => (long)b,
                ushort us => (long)us,
                uint ui => (long)ui,
                ulong ul => (long)ul,
                float f => Round ? (long)Math.Round(f) : (long)(f),
                double  d => Round ? (long)Math.Round(d) : (long)(d),
                decimal d => Round ? (long)Math.Round(d) : (long)d,
                _ => 0,
            };
        }

        private static string FormatOct(bool Alternate,
										int FieldLength,
										bool Left2Right,
										char Padding, object Value )
        {
            string w = string.Empty;
            string lengthFormat = "{0" + ( FieldLength != int.MinValue ?
                                            "," + ( Left2Right ?
                                                    "-" :
                                                    string.Empty ) + FieldLength.ToString() :
                                            string.Empty ) + "}";

            if ( IsNumericType( Value ) )
            {
                w = Convert.ToString( UnboxToLong( Value, true ), 8 );

                if ( Left2Right || Padding == ' ' )
                {
                    if ( Alternate && w != "0" )
                        w = "0" + w;
                    w = string.Format( lengthFormat, w );
                }
                else
                {
                    if ( FieldLength != int.MinValue )
                        w = w.PadLeft( FieldLength - ( Alternate && w != "0" ? 1 : 0 ), Padding );
                    if ( Alternate && w != "0" )
                        w = "0" + w;
                }
            }

            return w;
        }
        private static string FormatHex( string NativeFormat, bool Alternate,
                                            int FieldLength, int FieldPrecision,
                                            bool Left2Right,
                                            char Padding, object Value )
        {
            string w = string.Empty;
            string lengthFormat = "{0" + ( FieldLength != int.MinValue ?
                                            "," + ( Left2Right ?
                                                    "-" :
                                                    string.Empty ) + FieldLength.ToString() :
                                            string.Empty ) + "}";
            string numberFormat = "{0:" + NativeFormat + ( FieldPrecision != int.MinValue ?
                                            FieldPrecision.ToString() :
                                            string.Empty ) + "}";

            if ( IsNumericType( Value ) )
            {
                w = string.Format( numberFormat, Value );

                if ( Left2Right || Padding == ' ' )
                {
                    if ( Alternate )
                        w = ( NativeFormat == "x" ? "0x" : "0X" ) + w;
                    w = string.Format( lengthFormat, w );
                }
                else
                {
                    if ( FieldLength != int.MinValue )
                        w = w.PadLeft( FieldLength - ( Alternate ? 2 : 0 ), Padding );
                    if ( Alternate )
                        w = ( NativeFormat == "x" ? "0x" : "0X" ) + w;
                }
            }

            return w;
        }

        /// <summary>
        /// Converts the specified values boxed type to its correpsonding unsigned
        /// type.
        /// </summary>
        /// <param name="Value">The value.</param>
        /// <returns>A boxed numeric object whos type is unsigned.</returns>
        public static object ToUnsigned( object Value )
        {
			return Value switch
			{
				sbyte sb => (byte)sb,
				short s => (ushort)s,
				int i => (uint)i,
				long l => (ulong)l,
				byte _ => Value,
				ushort _ => Value,
				uint _ => Value,
				ulong _ => Value,
				float f => (UInt32)f,
				double d => (ulong)d,
				decimal d => (ulong)d,
				_ => null,
			};
		}

        /// <summary>
        /// Determines whether the specified value is positive.
        /// </summary>
        /// <param name="Value">The value.</param>
        /// <param name="ZeroIsPositive">if set to <c>true</c> treats 0 as positive.</param>
        /// <returns>
        /// 	<c>true</c> if the specified value is positive; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPositive( object Value, bool ZeroIsPositive )
        {
			return Value switch
			{
				sbyte sb => ZeroIsPositive ? sb >= 0 : sb > 0,
				short s => ZeroIsPositive ? s >= 0 : s > 0,
				int i => ZeroIsPositive ? i >= 0 : i > 0,
				long l => ZeroIsPositive ? l >= 0 : l > 0,
				float f => ZeroIsPositive ? f >= 0 : f > 0,
				double d => ZeroIsPositive ? d >= 0 : d > 0,
				decimal d => ZeroIsPositive ? d >= 0 : d > 0,
				byte b => ZeroIsPositive || b > 0,
				ushort us => ZeroIsPositive || us > 0,
				uint ui => ZeroIsPositive || ui > 0,
				ulong ul => ZeroIsPositive || ul > 0,
				char c => ZeroIsPositive || c != '\0',
				_ => false,
			};
		}

        /// <summary>
        /// Determines whether the specified value is of numeric type.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <returns>
        /// 	<c>true</c> if o is a numeric type; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNumericType( object o )
        {
            return o is byte ||
                o is sbyte ||
                o is short ||
                o is ushort ||
                o is int ||
                o is uint ||
                o is long ||
                o is ulong ||
                o is float ||
                o is double ||
                o is decimal ;
        }
    }
}
