using System;
using System.Collections.Generic;
using System.Threading;

namespace ACE.Entity.Models
{
    public static class PropertiesTextureMapExtensions
    {
        public static int GetCount(this IList<PropertiesTextureMap> value, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            try
            {
                return value.Count;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static List<PropertiesTextureMap> Clone(this IList<PropertiesTextureMap> value, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            try
            {
                return new List<PropertiesTextureMap>(value);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }


        public static void Add(this IList<PropertiesTextureMap> value, IList<PropertiesTextureMap> entries, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try
            {
                foreach (var entry in entries)
                    value.Add(entry);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
    }
}
