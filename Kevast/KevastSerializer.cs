using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
            Int32,

            String_Null,
            String_Empty,
            String_Length_8,
            String_Length_16,
            String_Length_32,

            DateTime_Unspecified,
            DateTime_Utc,
            DateTime_Local,

            Decimal_0,

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

            Guid,
            TimeSpan,
            DateTimeOffset,
            IntPtr,
            UIntPtr,

            Enumerable,
            Dictionary,

            ArrayOfBooleans,
            ArrayOfBytes,
            ArrayOfSBytes,
            ArrayOfDBNulls,
            ArrayOfChars,
            ArrayOfInt16,
            ArrayOfInt32,
            ArrayOfInt64,
            ArrayOfUInt16,
            ArrayOfUInt32,
            ArrayOfUInt64,
            ArrayOfDoubles,
            ArrayOfSingles,
            ArrayOfDecimals,
            ArrayOfDateTimes,
            ArrayOfGuids,
            ArrayOfTimeSpans,
            ArrayOfDateTimeOffsets,
            ArrayOfIntPtr,
            ArrayOfUIntPtr,
            ArrayOfStrings,
            Array,
        }

        public KevastSerializer(KevastPersistenceOptions? options)
        {
            Options = options;
        }

        public KevastPersistenceOptions? Options { get; }

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

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _isBlittable = new System.Collections.Concurrent.ConcurrentDictionary<Type, bool>();

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

        public virtual object? Read(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return null;
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
                    writeByte((byte)value);
                    return;

                case TypeCode.Byte:
                    writeCode(Code.Byte);
                    writeSByte((sbyte)value);
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

                    writeCode(Code.Int32);
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

                    bytes = Encoding.Unicode.GetBytes(str);
                    if (bytes.Length < 0x100)
                    {
                        writeCode(Code.String_Length_8);
                        writeByte((byte)bytes.Length);
                        writeBytes(bytes);
                        return;
                    }

                    if (bytes.Length < 0x10000)
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
                        writeCode(Code.Guid);
                        var guid = (Guid)value;
                        writeBytes(guid.ToByteArray());
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

                    if (type is IDictionary dictionary)
                    {
                        writeCode(Code.Dictionary);
                        return;
                    }

                    if (type.IsArray)
                    {
                        var et = type.GetElementType();
                        if (et != null)
                        {
                            var etc = Type.GetTypeCode(et);
                            switch (etc)
                            {
                                case TypeCode.DBNull:
                                    writeCode(Code.ArrayOfDBNulls);
                                    break;

                                case TypeCode.Boolean:
                                    writeCode(Code.ArrayOfBooleans);
                                    break;

                                case TypeCode.Char:
                                    writeCode(Code.ArrayOfChars);
                                    break;

                                case TypeCode.SByte:
                                    writeCode(Code.ArrayOfSBytes);
                                    break;

                                case TypeCode.Byte:
                                    writeCode(Code.ArrayOfBytes);
                                    break;

                                case TypeCode.Int16:
                                    writeCode(Code.ArrayOfInt16);
                                    break;

                                case TypeCode.UInt16:
                                    writeCode(Code.ArrayOfUInt16);
                                    break;

                                case TypeCode.Int32:
                                    writeCode(Code.ArrayOfInt32);
                                    break;

                                case TypeCode.UInt32:
                                    writeCode(Code.ArrayOfUInt32);
                                    break;

                                case TypeCode.Int64:
                                    writeCode(Code.ArrayOfInt64);
                                    break;

                                case TypeCode.UInt64:
                                    writeCode(Code.ArrayOfUInt64);
                                    break;

                                case TypeCode.Single:
                                    writeCode(Code.ArrayOfSingles);
                                    break;

                                case TypeCode.Double:
                                    writeCode(Code.ArrayOfDoubles);
                                    break;
                                
                                case TypeCode.Decimal:
                                    writeCode(Code.ArrayOfDecimals);
                                    break;

                                case TypeCode.DateTime:
                                    writeCode(Code.ArrayOfDateTimes);
                                    break;

                                case TypeCode.String:
                                    writeCode(Code.ArrayOfStrings);
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                    else if (type is IEnumerable enumerable)
                    {
                        writeCode(Code.Enumerable);
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
            void writeBytes(byte[] bytes) => stream.Write(bytes);
            void writeBytesSpan<T>(ReadOnlySpan<T> span) where T : struct => stream.Write(MemoryMarshal.AsBytes<T>(span));

            throw new NotSupportedException("Value '" + value + "' of type '" + value.GetType().FullName + "' is not supported.");
        }
    }
}
