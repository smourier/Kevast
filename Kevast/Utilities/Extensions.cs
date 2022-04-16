using System;
using System.IO;
using System.Text;

namespace Kevast.Utilities
{
    public static class Extensions
    {
        public static Encoding PlatformCompatibleUnicode => BitConverter.IsLittleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
        static bool IsPlatformCompatibleUnicode(this Encoding? encoding) => BitConverter.IsLittleEndian ? encoding?.CodePage == 1200 : encoding?.CodePage == 1201;

        public static Stream AsStream(this string? @string, Encoding? encoding = default) => (@string ?? throw new ArgumentNullException(nameof(@string))).AsMemory().AsStream(encoding);
        public static Stream AsStream(this ReadOnlyMemory<char> buffer, Encoding? encoding = default) =>
            (encoding ??= Encoding.UTF8).IsPlatformCompatibleUnicode() ? new UnicodeStringStream(buffer) : Encoding.CreateTranscodingStream(new UnicodeStringStream(buffer), PlatformCompatibleUnicode, encoding, false);
    }
}
