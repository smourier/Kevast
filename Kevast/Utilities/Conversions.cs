using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Kevast.Utilities
{
    public static class Conversions
    {
        public static string ToHex(this int i) => "0x" + i.ToString("X8");
        public static string? ToHex(this int? i) => i.HasValue ? "0x" + i.Value.ToString("X8") : null;
        public static string? ToHex(this long? i) => i.HasValue ? "0x" + i.Value.ToString("X16") : null;
        public static string ToHex(this long i) => "0x" + i.ToString("X16");

        public static string? GetAllMessagesWithDots(this Exception exception) => GetAllMessages(exception, s => s?.EndsWith(".") == true ? null : ". ");
        public static string? GetAllMessages(this Exception exception) => GetAllMessages(exception, s => Environment.NewLine);
        public static string? GetAllMessages(this Exception exception, Func<string, string?> separator)
        {
            if (exception == null)
                return null;

            var sb = new StringBuilder();
            AppendMessages(sb, exception, separator);
            return sb.ToString().Replace("..", ".").Nullify();
        }

        private static void AppendMessages(StringBuilder sb, Exception? e, Func<string, string?>? separator)
        {
            if (e == null)
                return;

            if (e is AggregateException agg)
            {
                foreach (var ex in agg.InnerExceptions)
                {
                    AppendMessages(sb, ex, separator);
                }
                return;
            }

            if (!(e is TargetInvocationException))
            {
                if (sb.Length > 0 && separator != null)
                {
                    var sep = separator(sb.ToString());
                    if (sep != null)
                    {
                        sb.Append(sep);
                    }
                }
                sb.Append(e.Message);
            }
            AppendMessages(sb, e.InnerException, separator);
        }

        public static IEnumerable<Exception> EnumerateAllExceptions(this Exception exception)
        {
            if (exception == null)
                yield break;

            yield return exception;
            if (exception is AggregateException agg)
            {
                foreach (var ae in agg.InnerExceptions)
                {
                    foreach (var child in EnumerateAllExceptions(ae))
                    {
                        yield return child;
                    }
                }
            }
            else
            {
                if (exception.InnerException != null)
                {
                    foreach (var child in EnumerateAllExceptions(exception.InnerException))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static bool TryReadStruct<T>(byte[] bytes, out object? value) where T : struct
        {
            if (!MemoryMarshal.TryRead<T>(bytes, out var tvalue))
            {
                value = default;
                return false;
            }

            value = tvalue;
            return true;
        }

        public static bool TryGetStruct(Type type, byte[] bytes, out object? value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var method = typeof(Conversions).GetMethod(nameof(TryReadStruct), BindingFlags.Static | BindingFlags.NonPublic);
            var gmethod = method!.MakeGenericMethod(type);
            var parameters = new object[2];
            parameters[0] = bytes;
            var b = (bool)gmethod.Invoke(null, parameters)!;
            value = parameters[1];
            return b;
        }

        private static byte[] GetByteArray<T>(T value) where T : struct => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
        public static byte[] GetBytes(object value)
        {
            var method = typeof(Conversions).GetMethod(nameof(GetByteArray), BindingFlags.Static | BindingFlags.NonPublic);
            var gmethod = method!.MakeGenericMethod(value.GetType());
            return (byte[])gmethod.Invoke(null, new object[] { value })!;
        }

        public static string? ToHexaDump(string text, Encoding? encoding = null)
        {
            if (text == null)
                return null;

            encoding ??= Encoding.Unicode;
            return ToHexaDump(encoding.GetBytes(text));
        }

        public static string? ToHexaDump(this byte[] bytes, string? prefix = null)
        {
            if (bytes == null)
                return null;

            return ToHexaDump(bytes, 0, bytes.Length, prefix, true);
        }

        public static string? ToHexaDump(this IntPtr ptr, int count) => ToHexaDump(ptr, 0, count);
        public static string? ToHexaDump(this IntPtr ptr, int offset, int count, string? prefix = null, bool addHeader = true)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var bytes = new byte[count];
            Marshal.Copy(ptr, bytes, offset, count);
            return ToHexaDump(bytes, 0, count, prefix, addHeader);
        }

        public static string? ToHexaDump(this byte[] bytes, int count) => ToHexaDump(bytes, 0, count);
        public static string? ToHexaDump(this byte[] bytes, int offset, int count, string? prefix = null, bool addHeader = true)
        {
            if (bytes == null)
                return null;

            if (offset < 0)
            {
                offset = 0;
            }

            if (count < 0)
            {
                count = bytes.Length;
            }

            if ((offset + count) > bytes.Length)
            {
                count = bytes.Length - offset;
            }

            var sb = new StringBuilder();
            if (addHeader)
            {
                sb.Append(prefix);
                //             0         1         2         3         4         5         6         7
                //             01234567890123456789012345678901234567890123456789012345678901234567890123456789
                sb.AppendLine("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F  0123456789ABCDEF");
                sb.AppendLine("--------  -----------------------------------------------  ----------------");
            }

            for (var i = 0; i < count; i += 16)
            {
                sb.Append(prefix);
                sb.AppendFormat("{0:X8}  ", i + offset);

                int j;
                for (j = 0; (j < 16) && ((i + j) < count); j++)
                {
                    sb.AppendFormat("{0:X2} ", bytes[i + j + offset]);
                }

                sb.Append(" ");
                if (j < 16)
                {
                    sb.Append(new string(' ', 3 * (16 - j)));
                }
                for (j = 0; j < 16 && (i + j) < count; j++)
                {
                    var b = bytes[i + j + offset];
                    if (b > 31 && b < 128)
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.Append('.');
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Wrap(this string input, int length, string? strictSeparator = null, Func<char, bool>? breakFunc = null) => string.Join(Environment.NewLine, WrapToLines(input, length, strictSeparator, breakFunc));
        public static IEnumerable<string> WrapToLines(this string input, int length, string? strictSeparator = null, Func<char, bool>? breakFunc = null)
        {
            if (input == null)
                return Enumerable.Empty<string>();

            var reader = new StringReader(input);
            return reader.WrapToLines(length, strictSeparator, breakFunc);
        }

        public static IEnumerable<string> WrapToLines(this TextReader reader, int length, string? strictSeparator = null, Func<char, bool>? breakFunc = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var sepLen = (strictSeparator?.Length).GetValueOrDefault();
            if (sepLen >= length)
                throw new ArgumentOutOfRangeException(nameof(strictSeparator));

            breakFunc ??= WordBreak;

            var sb = new StringBuilder();
            var maxLen = length - sepLen;
            int? lastSeparatorPos = null;
            do
            {
                var i = reader.Read();
                if (i < 0)
                {
                    if (sb.Length > 0)
                        yield return sb.ToString();

                    break;
                }

                var c = (char)i;
                if (WordBreak(c))
                {
                    lastSeparatorPos = sb.Length;
                }

                if (char.IsWhiteSpace(c))
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }

                if (sb.Length >= maxLen)
                {
                    if (lastSeparatorPos.HasValue)
                    {
                        var sbs = sb.ToString();
                        var str = sbs.Substring(0, lastSeparatorPos.Value).Trim();
                        if (str.Length > 0)
                            yield return str;

                        sb = new StringBuilder(sbs.Substring(lastSeparatorPos.Value));
                        lastSeparatorPos = null;
                    }
                    else
                    {
                        if (strictSeparator != null)
                        {
                            var str = sb.ToString().Nullify();
                            if (str != null)
                                yield return str + strictSeparator;

                            sb.Clear();
                            lastSeparatorPos = null;
                        }
                        // else continue
                    }
                }
            }
            while (true);
        }

        private static bool WordBreak(char c)
        {
            var cat = char.GetUnicodeCategory(c);
            switch (cat)
            {
                case UnicodeCategory.SpaceSeparator:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.Control:
                    return true;

                default:
                    return false;
            }
        }

        public static string? Truncate(this string input, int maxLength, string dotsSuffix = "...")
        {
            if (maxLength <= 0)
                throw new ArgumentException(null, nameof(maxLength));

            if (input == null || maxLength == int.MaxValue)
                return input;

            var dotsLen = (dotsSuffix?.Length).GetValueOrDefault();
            if ((input.Length + dotsLen) > maxLength)
                return input.Substring(0, maxLength - dotsLen) + dotsSuffix;

            return input;
        }

        private static readonly Dictionary<char, int> _romanMap = new Dictionary<char, int>() { { 'I', 1 }, { 'V', 5 }, { 'X', 10 }, { 'L', 50 }, { 'C', 100 }, { 'D', 500 }, { 'M', 1000 } };
        public static bool TryParseRoman(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            var number = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!_romanMap.TryGetValue(text[i], out var num))
                    return false;

                if ((i + 1) < text.Length)
                {
                    if (!_romanMap.TryGetValue(text[i + 1], out var num2))
                        return false;

                    if (num < num2)
                    {
                        number -= num;
                        continue;
                    }
                }

                number += num;
            }

            value = number;
            return true;
        }

        // A => 1
        // Z => 26
        // AA => 27
        // AB => 28
        // XFD => 16384
        public static bool TryParseExcelColumn(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            if (int.TryParse(text, out value) && value >= 0)
                return true;

            var number = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = char.ToLowerInvariant(text[i]);
                if (c < 'a' || c > 'z')
                    return false;

                number *= 26;
                var num = c - 'a';
                num++;
                number += num;
            }

            value = number;
            return true;
        }

        public static bool EqualsIgnoreCase(this string? thisString, string? text, bool trim = false)
        {
            if (trim)
            {
                thisString = thisString.Nullify();
                text = text.Nullify();
            }

            if (thisString == null)
                return text == null;

            if (text == null)
                return false;

            if (thisString.Length != text.Length)
                return false;

            return string.Compare(thisString, text, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static string? Nullify(this string? text)
        {
            if (text == null)
                return null;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var t = text.Trim();
            return t.Length == 0 ? null : t;
        }

        public static string? JoinWithDots(params object[] texts) => JoinWithDots(texts?.Select(t => t?.ToString()));
        public static string? JoinWithDots(this IEnumerable<string?>? texts)
        {
            if (texts == null)
                return null;

            var sb = new StringBuilder();
            foreach (var text in texts)
            {
                var t = text;
                if (t == null)
                    continue;

                while (t.StartsWith("."))
                {
                    t = t.Substring(1);
                }

                t = t.Nullify();
                if (t == null)
                    continue;

                if (sb.Length > 0)
                {
                    sb.Append(". ");
                }
                sb.Append(t);
            }
            return sb.ToString().Nullify();
        }

        public static string ComputeHashString(string text) => ComputeGuidHash(text).ToString("N");
        public static Guid ComputeGuidHash(string text)
        {
            if (text == null)
                return Guid.Empty;

            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static bool AreDictionaryEquals<T>(this IDictionary<string, T> dic1, IDictionary<string, T> dic2, IEqualityComparer<T>? comparer = null)
        {
            if (ReferenceEquals(dic1, dic2))
                return true;

            if (dic1 == null)
                return dic1 == null;

            if (dic2 == null)
                return false;

            if (dic1.Count != dic2.Count)
                return false;

            comparer ??= EqualityComparer<T>.Default;
            foreach (var kv in dic1)
            {
                if (!dic2.TryGetValue(kv.Key, out var value))
                    return false;

                if (!comparer.Equals(kv.Value, value))
                    return false;
            }

            foreach (var kv in dic2)
            {
                if (!dic1.TryGetValue(kv.Key, out var value))
                    return false;

                if (!comparer.Equals(kv.Value, value))
                    return false;
            }
            return true;
        }

        public static T? GetSegment<T>(this Uri uri, int index, T? defaultValue = default)
        {
            var seg = GetSegment(uri, index);
            if (seg == null)
                return defaultValue;

            return ChangeType<T>(seg, defaultValue);
        }

        public static string? GetSegment(this Uri? uri, int index) => GetSegments(uri).Skip(index)?.FirstOrDefault().Nullify();
        public static string? GetSegmentAfter(this Uri? uri, int index) => string.Join(Path.AltDirectorySeparatorChar.ToString(), GetSegments(uri).Skip(index));

        public static IEnumerable<string> GetSegments(this Uri? uri)
        {
            if (uri == null)
                yield break;

            foreach (var segment in uri.Segments)
            {
                if (segment.EndsWith("/"))
                    yield return segment.Substring(0, segment.Length - 1);
                else
                    yield return segment;
            }
        }

        public static new bool Equals(object o1, object o2)
        {
            if (o1 == null)
                return o2 == null;

            if (o2 == null)
                return false;

            if (object.Equals(o1, o2))
                return true;

            if (TryChangeType(o2, o1.GetType(), out var co2) && o1.Equals(co2))
                return true;

            if (TryChangeType(o1, o2.GetType(), out var co1) && o2.Equals(co1))
                return true;

            return false;
        }

        public static CultureInfo ParseCultureInfo(string? language, CultureInfo? defaultValue = null)
        {
            var culture = defaultValue ?? CultureInfo.CurrentCulture;
            language = language.Nullify();
            if (language != null)
            {
                try
                {
                    if (int.TryParse(language, out var lcid))
                    {
                        culture = CultureInfo.GetCultureInfo(lcid);
                    }
                    else
                    {
                        culture = CultureInfo.GetCultureInfo(language);
                    }
                }
                catch
                {
                }
            }
            return culture;
        }

        private static readonly char[] _enumSeparators = new char[] { ',', ';', '+', '|', ' ' };

        public static bool TryParseEnum(Type type, object? input, out object? value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum)
                throw new ArgumentException(null, nameof(type));

            if (input == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var stringInput = string.Format(CultureInfo.InvariantCulture, "{0}", input);
            stringInput = stringInput.Nullify();
            if (stringInput == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            if (stringInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(stringInput.Substring(2), NumberStyles.HexNumber, null, out ulong ulx))
                {
                    value = ToEnum(ulx.ToString(CultureInfo.InvariantCulture), type);
                    return true;
                }
            }

            var names = Enum.GetNames(type);
            if (names.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var values = Enum.GetValues(type);
            // some enums like System.CodeDom.MemberAttributes *are* flags but are not declared with Flags...
            if (!type.IsDefined(typeof(FlagsAttribute), true) && stringInput.IndexOfAny(_enumSeparators) < 0)
                return StringToEnum(type, names, values, stringInput, out value);

            // multi value enum
            var tokens = stringInput.Split(_enumSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            ulong ul = 0;
            foreach (var tok in tokens)
            {
                var token = tok.Nullify(); // NOTE: we don't consider empty tokens as errors
                if (token == null)
                    continue;

                if (!StringToEnum(type, names, values, token, out var tokenValue))
                {
                    value = Activator.CreateInstance(type);
                    return false;
                }

                ulong tokenUl;
                switch (Convert.GetTypeCode(tokenValue))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.SByte:
                        tokenUl = (ulong)Convert.ToInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;

                    default:
                        tokenUl = Convert.ToUInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;
                }

                ul |= tokenUl;
            }
            value = Enum.ToObject(type, ul);
            return true;
        }

        public static object ToEnum(string text, Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            TryParseEnum(enumType, text, out var value);
            return value!;
        }

        private static bool StringToEnum(Type type, string[] names, Array values, string input, out object value)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].EqualsIgnoreCase(input))
                {
                    value = values.GetValue(i)!;
                    return true;
                }
            }

            for (var i = 0; i < values.GetLength(0); i++)
            {
                var valuei = values.GetValue(i)!;
                if (input.Length > 0 && input[0] == '-')
                {
                    var ul = (long)EnumToUInt64(valuei);
                    if (ul.ToString().EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
                else
                {
                    var ul = EnumToUInt64(valuei);
                    if (ul.ToString().EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
            }

            if (char.IsDigit(input[0]) || input[0] == '-' || input[0] == '+')
            {
                var obj = EnumToObject(type, input);
                if (obj == null)
                {
                    value = Activator.CreateInstance(type)!;
                    return false;
                }
                value = obj;
                return true;
            }

            value = Activator.CreateInstance(type)!;
            return false;
        }

        public static object EnumToObject(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var underlyingType = Enum.GetUnderlyingType(enumType);
            if (underlyingType == typeof(long))
                return Enum.ToObject(enumType, ChangeType<long>(value));

            if (underlyingType == typeof(ulong))
                return Enum.ToObject(enumType, ChangeType<ulong>(value));

            if (underlyingType == typeof(int))
                return Enum.ToObject(enumType, ChangeType<int>(value));

            if ((underlyingType == typeof(uint)))
                return Enum.ToObject(enumType, ChangeType<uint>(value));

            if (underlyingType == typeof(short))
                return Enum.ToObject(enumType, ChangeType<short>(value));

            if (underlyingType == typeof(ushort))
                return Enum.ToObject(enumType, ChangeType<ushort>(value));

            if (underlyingType == typeof(byte))
                return Enum.ToObject(enumType, ChangeType<byte>(value));

            if (underlyingType == typeof(sbyte))
                return Enum.ToObject(enumType, ChangeType<sbyte>(value));

            throw new ArgumentException(null, nameof(enumType));
        }

        public static ulong EnumToUInt64(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var typeCode = Convert.GetTypeCode(value);
            switch (typeCode)
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.String:
                default:
                    return ChangeType<ulong>(value, 0, CultureInfo.InvariantCulture);
            }
        }

        public static object? ChangeType(object? input, Type conversionType, object? defaultValue = null, IFormatProvider? provider = null)
        {
            if (!TryChangeType(input, conversionType, provider, out object? value))
                return defaultValue;

            return value;
        }

        public static T? ChangeType<T>(object? input, T? defaultValue = default, IFormatProvider? provider = null)
        {
            if (!TryChangeType(input, provider, out T? value))
                return defaultValue;

            return value;
        }

        public static bool TryChangeType<T>(object? input, out T? value) => TryChangeType(input, null, out value);
        public static bool TryChangeType<T>(object? input, IFormatProvider? provider, out T? value)
        {
            if (!TryChangeType(input, typeof(T), provider, out object? tvalue))
            {
                value = default;
                return false;
            }

            value = (T)tvalue!;
            return true;
        }

        public static bool TryChangeType(object? input, Type conversionType, out object? value) => TryChangeType(input, conversionType, null, out value);
        public static bool TryChangeType(object? input, Type conversionType, IFormatProvider? provider, out object? value)
        {
            if (conversionType == null)
                throw new ArgumentNullException(nameof(conversionType));

            if (conversionType == typeof(object))
            {
                value = input;
                return true;
            }

            value = conversionType.IsValueType ? Activator.CreateInstance(conversionType) : null;
            if (input == null)
                return !conversionType.IsValueType;

            var inputType = input.GetType();
            if (conversionType.IsAssignableFrom(inputType))
            {
                value = input;
                return true;
            }

            if (conversionType.IsEnum)
                return TryParseEnum(conversionType, input, out value);

            if (conversionType == typeof(Guid))
            {
                var svalue = string.Format(provider, "{0}", input).Nullify();
                if (svalue != null && Guid.TryParse(svalue, out Guid guid))
                {
                    value = guid;
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(Type))
            {
                var typeName = string.Format(provider, "{0}", input).Nullify();
                if (typeName == null)
                    return false;

                var type = Type.GetType(typeName, false);
                if (type == null)
                    return false;

                value = type;
                return true;
            }

            if (conversionType == typeof(IntPtr))
            {
                if (IntPtr.Size == 8)
                {
                    if (TryChangeType(input, provider, out long l))
                    {
                        value = new IntPtr(l);
                        return true;
                    }
                }
                else if (TryChangeType(input, provider, out int i))
                {
                    value = new IntPtr(i);
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(int))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((int)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((int)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((int)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((int)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(long))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((long)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((long)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((long)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((long)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(short))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((short)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((short)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((short)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((short)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(sbyte))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((sbyte)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((sbyte)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((sbyte)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((sbyte)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(uint))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((uint)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((uint)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((uint)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((uint)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ulong))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ulong)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ulong)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ulong)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ulong)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ushort))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ushort)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ushort)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ushort)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ushort)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(byte))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((byte)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((byte)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((byte)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((byte)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(DateTime))
            {
                if (input is double dbl)
                {
                    try
                    {
                        value = DateTime.FromOADate(dbl);
                        return true;
                    }
                    catch
                    {
                        value = DateTime.MinValue;
                        return false;
                    }
                }
            }

            if (conversionType == typeof(DateTimeOffset))
            {
                if (input is double dbl)
                {
                    try
                    {
                        value = new DateTimeOffset(DateTime.FromOADate(dbl));
                        return true;
                    }
                    catch
                    {
                        value = DateTimeOffset.MinValue;
                        return false;
                    }
                }
            }

            if (conversionType == typeof(bool))
            {
                if (TryChangeType<long>(input, out var i))
                {
                    value = i != 0;
                    return true;
                }
            }

            var nullable = conversionType.IsGenericType && conversionType.GetGenericTypeDefinition() == typeof(Nullable<>);
            if (nullable)
            {
                if (input == null || string.Empty.Equals(input))
                {
                    value = null;
                    return true;
                }

                var type = conversionType.GetGenericArguments()[0];
                if (TryChangeType(input, type, provider, out var vtValue))
                {
                    var nullableType = typeof(Nullable<>).MakeGenericType(type);
                    value = Activator.CreateInstance(nullableType, vtValue);
                    return true;
                }

                value = null;
                return false;
            }

            if (input is IConvertible convertible)
            {
                try
                {
                    value = convertible.ToType(conversionType, provider);
                    return true;
                }
                catch
                {
                    // do nothing
                    return false;
                }
            }

            return false;
        }

        public static T? GetValue<T>(this IDictionary<string, object> dictionary, string key, T? defaultValue = default, IFormatProvider? provider = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out object? o))
                return defaultValue;

            return ChangeType(o, defaultValue, provider);
        }

        public static string? GetNullifiedValue(this IDictionary<string, string> dictionary, string key, string? defaultValue = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out string? str))
                return defaultValue;

            return str.Nullify();
        }

        public static string? GetNullifiedValue(this IDictionary<string, object> dictionary, string key, IFormatProvider? provider = null, string? defaultValue = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var obj))
                return defaultValue;

            if (obj == null)
                return null;

            if (obj is string s)
                return s.Nullify();

            if (provider == null)
                return string.Format("{0}", obj).Nullify();

            return string.Format(provider, "{0}", obj).Nullify();
        }

        public static T? GetValue<T>(this IDictionary<string, string> dictionary, string key, T? defaultValue = default, IFormatProvider? provider = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out string? str))
                return defaultValue;

            return ChangeType(str, defaultValue, provider);
        }

        public static bool TryGetValueByPath<T>(this IDictionary<string, object> dictionary, string path, out T? value)
        {
            if (!TryGetValueByPath(dictionary, path, out object? obj))
            {
                value = default;
                return false;
            }

            return TryChangeType(obj, out value);
        }

        public static bool TryGetValueByPath(this IDictionary<string, object> dictionary, string path, out object? value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            value = null;
            var segments = path.Split('.');
            var current = dictionary;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i].Nullify();
                if (segment == null)
                    return false;

                if (!current.TryGetValue(segment, out var newElement))
                    return false;

                // last?
                if (i == segments.Length - 1)
                {
                    value = newElement;
                    return true;
                }
                current = newElement as IDictionary<string, object>;
                if (current == null)
                    break;
            }
            return false;
        }
    }
}
