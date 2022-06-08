using System;
using System.IO;
using System.Linq;
using Kevast.Utilities;

namespace Kevast
{
    public partial class KevastDictionary<TKey, TValue>
    {
        private static readonly byte[] _signatureV1 = new byte[] { 0x4B, 0x56, 0x53, 0x31 }; // "KVS1"

        public static KevastDictionary<TKey, TValue> Load(string filePath, KevastPersistenceOptions? options = null)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Load(file, options);
        }

        public static KevastDictionary<TKey, TValue> Load(Stream stream, KevastPersistenceOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var sig = new byte[4];
            var dic = new KevastDictionary<TKey, TValue>();
            if (stream.Read(sig, 0, sig.Length) == sig.Length)
            {
                if (sig.SequenceEqual(_signatureV1))
                {
                    var serializer = options?.Serializer ?? new KevastSerializer(options);
                    do
                    {
                        if (!serializer.TryRead<TKey>(stream, out var key) || key == null || !serializer.TryRead<TValue>(stream, out var value))
                            break;

                        dic[key] = value!;
                    }
                    while (true);
                }
            }
            return dic;
        }

        public void Save(string filePath, KevastPersistenceOptions? options = null)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            Save(file, options);
        }

        public virtual void Save(Stream stream, KevastPersistenceOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var serializer = options?.Serializer ?? new KevastSerializer(options);
            stream.Write(_signatureV1);
            var kvs = ToArray();
            foreach (var kv in kvs)
            {
                serializer.Write(stream, kv.Key);
                serializer.Write(stream, kv.Value);
            }
        }
    }
}
