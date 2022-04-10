using System;
using System.Collections.Generic;

namespace Kevast
{
    public partial class KevastDictionary<TKey, TValue>
    {
        public KeyValuePair<TKey, TValue>[] ToArrayAndClear()
        {
            var locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                var count = 0;
                var countPerLock = _tables._countPerLock;
                for (var i = 0; i < countPerLock.Length; i++)
                {
                    checked
                    {
                        count += countPerLock[i];
                    }
                }
                if (count == 0)
                    return Array.Empty<KeyValuePair<TKey, TValue>>();

                var array = new KeyValuePair<TKey, TValue>[count];
                CopyToPairs(array, 0);

                var tables = _tables;
                var newTables = new Tables(new Node[DefaultCapacity], tables._locks, new int[tables._countPerLock.Length]);
                _tables = newTables;
                _budget = Math.Max(1, newTables._buckets.Length / newTables._locks.Length);
                return array;
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }
    }
}
