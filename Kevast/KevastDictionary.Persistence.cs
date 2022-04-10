using System;
using System.IO;

namespace Kevast
{
    public partial class KevastDictionary<TKey, TValue>
    {
        private static readonly byte[] _signature = new byte[] { 0xDE, 0xAD, 0xC0, 0x01 };

        private Node[] CopyToNodes()
        {
            var locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);
                var nodes = new Node[_tables._buckets.Length];
                Array.Copy(_tables._buckets, nodes, nodes.Length);
                return nodes;
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

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

            options ??= new KevastPersistenceOptions();

            return new KevastDictionary<TKey, TValue>();
        }

        public virtual void Save(string filePath, KevastPersistenceOptions? options = null)
        {
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            using var file = File.OpenWrite(filePath);
            Save(file, options);
        }

        public virtual void Save(Stream stream, KevastPersistenceOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var serializer = options?.Serializer ?? new KevastSerializer(options);
            stream.Write(_signature, 0, 4);
            var nodes = CopyToNodes();
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                serializer.Write(stream, node._key);
                serializer.Write(stream, node._value);
            }
            stream.Write(_signature, 0, 4);
        }
    }
}
