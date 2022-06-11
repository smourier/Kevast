using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Kevast.Utilities;

namespace Kevast
{
    public class KevastSerializer : IKevastSerializer
    {
        private enum Code : byte
        {
            Null,
            Empty,
            DbNull,

            False,
            True,

            Int32_0,
            Int32_P1,
            Int32_M1,
            Int32_8Bits,
            Int32_16Bits,
            Int32_32Bits,

            String_Null,
            String_Empty,
            String_Length_8,
            String_Length_16,
            String_Length_32,

            DateTime_Unspecified,
            DateTime_Utc,
            DateTime_Local,

            Decimal_0,
            Decimal,

            Byte,
            SByte,
            Char,
            Int16,
            UInt16,
            UInt32,
            Int64,
            UInt64,
            Single,
            Double,

            Guid_Empty,
            Guid,
            TimeSpan,
            DateTimeOffset,
            IntPtr,
            UIntPtr,

            Enumerable,
            Terminator,
            List,
            Dictionary_String_Object,
            Dictionary,
            Rank1Array,
            ByteArray,

            Blittable,
        }

        public KevastSerializer(KevastPersistenceOptions? options = null)
        {
            Options = options;
        }

        public KevastPersistenceOptions? Options { get; }
        public virtual Encoding StringEncoding => Encoding.UTF8;

        protected virtual bool IsBlittableType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsValueType)
                return false;

            var tc = Type.GetTypeCode(type);
            switch (tc)
            {
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                    return true;

                default:
                    if (type == typeof(IntPtr) || type == typeof(UIntPtr) ||
                        type == typeof(Guid) || type == typeof(TimeSpan) ||
                        type == typeof(DateTimeOffset))
                        return true;

                    break;
            }
            return IsBlittableCache(type);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _isBlittable = new();

        private static bool IsBlittableCache(Type type)
        {
            if (!_isBlittable.TryGetValue(type, out var result))
            {
                result = IsBlittable(type);
                _isBlittable[type] = result;
            }
            return result;
        }

        private static bool IsBlittable(Type type)
        {
            if (type.IsArray)
            {
                var elem = type.GetElementType();
                if (elem == null)
                    return false;

                return elem.IsValueType && IsBlittable(elem);
            }

            try
            {
                GCHandle.Alloc(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type), GCHandleType.Pinned).Free();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Encoding GetStringEncoding()
        {
            var enc = StringEncoding;
            if (enc == null)
                throw new InvalidOperationException(nameof(StringEncoding) + " property cannot be null.");

            return enc;
        }

        public virtual bool TryRead(Stream stream, out object? value)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var i = stream.ReadByte();
            if (i < 0)
            {
                value = null;
                return false;
            }

            int len;
            var code = (Code)i;
            switch (code)
            {
                case Code.Empty:
                case Code.Null:
                case Code.DbNull:
                case Code.String_Null:
                    value = null;
                    return true;

                case Code.Terminator:
                    value = code;
                    return true;

                case Code.True:
                    value = true;
                    return true;

                case Code.False:
                    value = false;
                    return true;

                case Code.Int32_0:
                    value = 0;
                    return true;

                case Code.Int32_P1:
                    value = 1;
                    return true;

                case Code.Int32_M1:
                    value = -1;
                    return true;

                case Code.Int32_32Bits:
                    Span<byte> i32 = stackalloc byte[4];
                    if (stream.Read(i32) != 4)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToInt32(i32);
                    return true;

                case Code.Int32_16Bits:
                    Span<byte> i32_16 = stackalloc byte[2];
                    if (stream.Read(i32_16) != 2)
                    {
                        value = null;
                        return false;
                    }

                    value = (int)BitConverter.ToInt16(i32_16);
                    return true;

                case Code.Int32_8Bits:
                    var i32_8 = stream.ReadByte();
                    if (i32_8 < 0)
                    {
                        value = null;
                        return false;
                    }

                    value = (int)(sbyte)i32_8;
                    return true;

                case Code.String_Empty:
                    value = string.Empty;
                    return true;

                case Code.String_Length_8:
                    var sb8 = stream.ReadByte();
                    if (sb8 < 0)
                    {
                        value = null;
                        return false;
                    }

                    Span<byte> s8 = stackalloc byte[sb8];
                    if (stream.Read(s8) != sb8)
                    {
                        value = null;
                        return false;
                    }

                    value = GetStringEncoding().GetString(s8);
                    return true;

                case Code.String_Length_16:
                    var sb16 = stream.ReadByte();
                    if (sb16 < 0)
                    {
                        value = null;
                        return false;
                    }

                    Span<byte> s16 = stackalloc byte[sb16];
                    if (stream.Read(s16) != sb16)
                    {
                        value = null;
                        return false;
                    }

                    value = GetStringEncoding().GetString(s16);
                    return true;

                case Code.String_Length_32:
                    var sb32 = stream.ReadByte();
                    if (sb32 < 0)
                    {
                        value = null;
                        return false;
                    }

                    Span<byte> s32 = stackalloc byte[sb32];
                    if (stream.Read(s32) != sb32)
                    {
                        value = null;
                        return false;
                    }

                    value = GetStringEncoding().GetString(s32);
                    return true;

                case Code.Guid_Empty:
                    value = Guid.Empty;
                    return true;

                case Code.Guid:
                    Span<byte> gb = stackalloc byte[16];
                    if (stream.Read(gb) != 16)
                    {
                        value = null;
                        return false;
                    }

                    value = new Guid(gb);
                    return true;

                case Code.UInt32:
                    Span<byte> ui32 = stackalloc byte[4];
                    if (stream.Read(ui32) != 4)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToUInt32(ui32);
                    return true;

                case Code.Int64:
                    Span<byte> i64 = stackalloc byte[8];
                    if (stream.Read(i64) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToInt64(i64);
                    return true;

                case Code.UInt64:
                    Span<byte> ui64 = stackalloc byte[8];
                    if (stream.Read(ui64) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToUInt64(ui64);
                    return true;

                case Code.Single:
                    Span<byte> single = stackalloc byte[4];
                    if (stream.Read(single) != 4)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToSingle(single);
                    return true;

                case Code.Double:
                    Span<byte> dbl = stackalloc byte[8];
                    if (stream.Read(dbl) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToDouble(dbl);
                    return true;

                case Code.Char:
                    Span<byte> c = stackalloc byte[2];
                    if (stream.Read(c) != 2)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToChar(c);
                    return true;

                case Code.DateTime_Local:
                    Span<byte> dtl = stackalloc byte[8];
                    if (stream.Read(dtl) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = new DateTime(BitConverter.ToInt64(dtl), DateTimeKind.Local);
                    return true;

                case Code.DateTime_Utc:
                    Span<byte> utc = stackalloc byte[8];
                    if (stream.Read(utc) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = new DateTime(BitConverter.ToInt64(utc), DateTimeKind.Utc);
                    return true;

                case Code.DateTime_Unspecified:
                    Span<byte> dtu = stackalloc byte[8];
                    if (stream.Read(dtu) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = new DateTime(BitConverter.ToInt64(dtu), DateTimeKind.Unspecified);
                    return true;

                case Code.DateTimeOffset:
                    Span<byte> dto = stackalloc byte[16];
                    if (stream.Read(dto) != 16)
                    {
                        value = null;
                        return false;
                    }

                    value = new DateTimeOffset(BitConverter.ToInt64(dto), new TimeSpan(BitConverter.ToInt64(dto.Slice(8))));
                    return true;

                case Code.TimeSpan:
                    Span<byte> ts = stackalloc byte[8];
                    if (stream.Read(ts) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = new TimeSpan(BitConverter.ToInt64(ts));
                    return true;

                case Code.Decimal_0:
                    value = 0m;
                    return true;

                case Code.Decimal:
                    Span<int> dec = stackalloc int[4];
                    if (stream.Read(MemoryMarshal.AsBytes(dec)) != 4 * 4)
                    {
                        value = null;
                        return false;
                    }

                    value = new decimal(dec);
                    return true;

                case Code.SByte:
                    var sb = stream.ReadByte();
                    if (sb < 0)
                    {
                        value = null;
                        return false;
                    }

                    value = (sbyte)sb;
                    return true;

                case Code.Byte:
                    var b = stream.ReadByte();
                    if (b < 0)
                    {
                        value = null;
                        return false;
                    }

                    value = (byte)b;
                    return true;

                case Code.Int16:
                    Span<byte> i16 = stackalloc byte[2];
                    if (stream.Read(i16) != 2)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToInt16(i16);
                    return true;

                case Code.UInt16:
                    Span<byte> ui16 = stackalloc byte[2];
                    if (stream.Read(ui16) != 2)
                    {
                        value = null;
                        return false;
                    }

                    value = BitConverter.ToUInt16(ui16);
                    return true;

                case Code.IntPtr:
                    Span<byte> ip = stackalloc byte[8];
                    if (stream.Read(ip) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = (IntPtr)BitConverter.ToInt64(ip);
                    return true;

                case Code.UIntPtr:
                    Span<byte> uip = stackalloc byte[8];
                    if (stream.Read(uip) != 8)
                    {
                        value = null;
                        return false;
                    }

                    value = (UIntPtr)BitConverter.ToInt64(uip);
                    return true;

                case Code.Enumerable:
                    var enumList = new List<object?>();
                    do
                    {
                        if (!TryRead(stream, out var evalue))
                        {
                            value = null;
                            return false;
                        }

                        if (Code.Terminator.Equals(evalue))
                        {
                            value = enumList;
                            return true;
                        }

                        enumList.Add(evalue);
                    }
                    while (true);

                case Code.List:
                    if (this.TryRead(stream, out len))
                    {
                        var list = new List<object?>(len);
                        for (var li = 0; li < len; li++)
                        {
                            if (!TryRead(stream, out var item))
                            {
                                value = null;
                                return false;
                            }

                            list.Add(item);
                        }

                        value = list;
                        return true;
                    }
                    break;

                case Code.Dictionary:
                    if (this.TryRead(stream, out len))
                    {
                        var dic = new Dictionary<object, object?>(len);
                        for (var li = 0; li < len; li++)
                        {
                            if (!TryRead(stream, out var itemKey) || itemKey == null)
                            {
                                value = null;
                                return false;
                            }

                            if (!TryRead(stream, out var itemValue))
                            {
                                value = null;
                                return false;
                            }

                            dic[itemKey] = itemValue;
                        }

                        value = dic;
                        return true;
                    }
                    break;

                case Code.Dictionary_String_Object:
                    if (this.TryRead(stream, out len))
                    {
                        var dic = new Dictionary<string, object?>(len);
                        for (var li = 0; li < len; li++)
                        {
                            if (!this.TryRead<string>(stream, out var itemKey) || itemKey == null)
                            {
                                value = null;
                                return false;
                            }

                            if (!TryRead(stream, out var itemValue))
                            {
                                value = null;
                                return false;
                            }

                            dic[itemKey] = itemValue;
                        }

                        value = dic;
                        return true;
                    }
                    break;

                case Code.Rank1Array:
                    if (this.TryRead(stream, out len))
                    {
                        var list = new object?[len];
                        for (var li = 0; li < len; li++)
                        {
                            if (!TryRead(stream, out var item))
                            {
                                value = null;
                                return false;
                            }

                            list[li] = item;
                        }

                        value = list;
                        return true;
                    }
                    break;

                case Code.ByteArray:
                    if (this.TryRead(stream, out len))
                    {
                        var bytes = new byte[len];
                        if (stream.Read(bytes) == len)
                        {
                            value = bytes;
                            return true;
                        }
                    }
                    break;

                case Code.Blittable:
                    if (this.TryRead<string>(stream, out var typeName) && typeName != null)
                    {
                        var type = Type.GetType(typeName, false);
                        if (type != null && this.TryRead<byte[]>(stream, out var bytes) && bytes != null)
                            return Conversions.TryGetStruct(type, bytes, out value);
                    }
                    break;
            }

            value = null;
            return false;
        }

        public virtual void Write(Stream stream, object? value)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (value == null)
            {
                writeCode(Code.Null);
                return;
            }

            byte[] bytes;
            var type = value.GetType();
            var tc = Type.GetTypeCode(type);
            switch (tc)
            {
                case TypeCode.Empty:
                    writeCode(Code.Empty);
                    return;

                case TypeCode.DBNull:
                    writeCode(Code.DbNull);
                    return;

                case TypeCode.Boolean:
                    writeBoolean((bool)value);
                    return;

                case TypeCode.Char:
                    writeCode(Code.Char);
                    writeChar((char)value);
                    return;

                case TypeCode.SByte:
                    writeCode(Code.SByte);
                    writeSByte((sbyte)value);
                    return;

                case TypeCode.Byte:
                    writeCode(Code.Byte);
                    writeByte((byte)value);
                    return;

                case TypeCode.Int16:
                    writeCode(Code.Int16);
                    writeInt16((short)value);
                    return;

                case TypeCode.UInt16:
                    writeCode(Code.UInt16);
                    writeUInt16((ushort)value);
                    return;

                case TypeCode.Int32:
                    var i32 = (int)value;
                    if (i32 == 0)
                    {
                        writeCode(Code.Int32_0);
                        return;
                    }

                    if (i32 == 1)
                    {
                        writeCode(Code.Int32_P1);
                        return;
                    }

                    if (i32 == -1)
                    {
                        writeCode(Code.Int32_M1);
                        return;
                    }

                    if (i32 >= sbyte.MinValue && i32 <= sbyte.MaxValue)
                    {
                        writeCode(Code.Int32_8Bits);
                        writeSByte((sbyte)(int)value);
                        return;
                    }

                    if (i32 >= short.MinValue && i32 <= short.MaxValue)
                    {
                        writeCode(Code.Int32_16Bits);
                        writeInt16((short)(int)value);
                        return;
                    }

                    writeCode(Code.Int32_32Bits);
                    writeInt32((int)value);
                    return;

                case TypeCode.UInt32:
                    writeCode(Code.UInt32);
                    writeUInt32((uint)value);
                    return;

                case TypeCode.Int64:
                    writeCode(Code.Int64);
                    writeInt64((long)value);
                    return;

                case TypeCode.UInt64:
                    writeCode(Code.UInt64);
                    writeUInt64((ulong)value);
                    return;

                case TypeCode.Single:
                    writeCode(Code.Single);
                    writeSingle((float)value);
                    return;

                case TypeCode.Double:
                    writeCode(Code.Double);
                    writeDouble((double)value);
                    return;

                case TypeCode.Decimal:
                    var dec = (decimal)value;
                    if (dec == 0)
                    {
                        writeCode(Code.Decimal_0);
                        return;
                    }

                    Span<int> span = stackalloc int[4];
                    decimal.GetBits(dec, span);
                    writeCode(Code.Decimal);
                    writeBytesSpan<int>(span);
                    return;

                case TypeCode.DateTime:
                    var dt = (DateTime)value;
                    switch (dt.Kind)
                    {
                        case DateTimeKind.Unspecified:
                            writeCode(Code.DateTime_Unspecified);
                            break;

                        case DateTimeKind.Utc:
                            writeCode(Code.DateTime_Utc);
                            break;

                        case DateTimeKind.Local:
                            writeCode(Code.DateTime_Local);
                            break;
                    }

                    writeInt64(dt.Ticks);
                    return;

                case TypeCode.String:
                    var str = (string)value;
                    if (str == null)
                    {
                        writeCode(Code.String_Null);
                        return;
                    }

                    if (str.Length == 0)
                    {
                        writeCode(Code.String_Empty);
                        return;
                    }

                    bytes = GetStringEncoding().GetBytes(str);
                    if (bytes.Length <= byte.MaxValue)
                    {
                        writeCode(Code.String_Length_8);
                        writeByte((byte)bytes.Length);
                        writeBytes(bytes);
                        return;
                    }

                    if (bytes.Length <= ushort.MaxValue)
                    {
                        writeCode(Code.String_Length_16);
                        writeUInt16((ushort)bytes.Length);
                        writeBytes(bytes);
                        return;
                    }

                    writeCode(Code.String_Length_32);
                    writeInt32(bytes.Length);
                    writeBytes(bytes);
                    return;

                default:
                    if (type == typeof(Guid))
                    {
                        var guid = (Guid)value;
                        if (guid == Guid.Empty)
                        {
                            writeCode(Code.Guid_Empty);
                            return;
                        }

                        writeCode(Code.Guid);
                        writeBytes(guid.ToByteArray());
                        return;
                    }

                    if (type == typeof(byte[]))
                    {
                        writeCode(Code.ByteArray);
                        var byteArray = (byte[])value;
                        Write(stream, byteArray.Length);
                        writeBytes(byteArray);
                        return;
                    }

                    if (type == typeof(TimeSpan))
                    {
                        writeCode(Code.TimeSpan);
                        var ts = (TimeSpan)value;
                        writeInt64(ts.Ticks);
                        return;
                    }

                    if (type == typeof(DateTimeOffset))
                    {
                        writeCode(Code.DateTimeOffset);
                        var dto = (DateTimeOffset)value;
                        writeInt64(dto.Ticks);
                        writeInt64(dto.Offset.Ticks);
                        return;
                    }

                    if (type == typeof(IntPtr))
                    {
                        writeCode(Code.IntPtr);
                        var dto = (IntPtr)value;
                        writeInt64(dto.ToInt64());
                        return;
                    }

                    if (type == typeof(UIntPtr))
                    {
                        writeCode(Code.UIntPtr);
                        var dto = (UIntPtr)value;
                        writeUInt64(dto.ToUInt64());
                        return;
                    }

                    if (value is IDictionary dictionary)
                    {
                        if (dictionary is IReadOnlyDictionary<string, object> || dictionary is IDictionary<string, object>)
                        {
                            writeCode(Code.Dictionary_String_Object);
                        }
                        else
                        {
                            writeCode(Code.Dictionary);
                        }
                        Write(stream, dictionary.Count);
                        foreach (DictionaryEntry kv in dictionary)
                        {
                            Write(stream, kv.Key);
                            Write(stream, kv.Value);
                        }
                        return;
                    }

                    if (value is IList list)
                    {
                        writeCode(Code.List);
                        Write(stream, list.Count);
                        foreach (var item in list)
                        {
                            Write(stream, item);
                        }
                        return;
                    }

                    if (type.IsArray)
                    {
                        var l = new List<string>();
                        var array = (Array)value;
                        if (array.Rank == 1)
                        {
                            writeCode(Code.Rank1Array);
                            Write(stream, array.Length);
                            for (var i = 0; i < array.Length; i++)
                            {
                                Write(stream, array.GetValue(i));
                            }
                            return;
                        }
                        break;
                    }

                    if (value is IEnumerable enumerable)
                    {
                        writeCode(Code.Enumerable);
                        foreach (var item in enumerable)
                        {
                            Write(stream, item);
                        }
                        writeCode(Code.Terminator);
                        return;
                    }

                    if (IsBlittable(type))
                    {
                        writeCode(Code.Blittable);
                        Write(stream, type.AssemblyQualifiedName);
                        var blit = Conversions.GetBytes(value);
                        Write(stream, blit);
                        return;
                    }

                    break;
            }

            void writeBoolean(bool value) => stream.WriteByte(value ? (byte)Code.True : (byte)Code.False);
            void writeByte(byte value) => stream.WriteByte(value);
            void writeSByte(sbyte value) => stream.WriteByte((byte)value);
            void writeChar(char value) => stream.Write(BitConverter.GetBytes(value));
            void writeUInt16(ushort value) => stream.Write(BitConverter.GetBytes(value));
            void writeInt16(short value) => stream.Write(BitConverter.GetBytes(value));
            void writeUInt32(uint value) => stream.Write(BitConverter.GetBytes(value));
            void writeInt32(int value) => stream.Write(BitConverter.GetBytes(value));
            void writeUInt64(ulong value) => stream.Write(BitConverter.GetBytes(value));
            void writeInt64(long value) => stream.Write(BitConverter.GetBytes(value));
            void writeDouble(double value) => stream.Write(BitConverter.GetBytes(value));
            void writeSingle(float value) => stream.Write(BitConverter.GetBytes(value));
            void writeCode(Code code) => writeByte((byte)code);
            void writeBytes(ReadOnlySpan<byte> bytes) => stream.Write(bytes);
            void writeBytesSpan<T>(ReadOnlySpan<T> span) where T : struct => stream.Write(MemoryMarshal.AsBytes<T>(span));

            throw new NotSupportedException("Value '" + value + "' of type '" + value.GetType().FullName + "' is not supported.");
        }
    }
}
