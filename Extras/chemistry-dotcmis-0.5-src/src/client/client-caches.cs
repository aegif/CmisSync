/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using DotCMIS.Util;

namespace DotCMIS.Client.Impl.Cache
{
    /// <summary>
    /// Client cache interface.
    /// </summary>
    public interface ICache
    {
        void Initialize(ISession session, IDictionary<string, string> parameters);
        bool ContainsId(string objectId, string cacheKey);
        bool ContainsPath(string path, string cacheKey);
        void Put(ICmisObject cmisObject, string cacheKey);
        void PutPath(string path, ICmisObject cmisObject, string cacheKey);
        ICmisObject GetById(string objectId, string cacheKey);
        ICmisObject GetByPath(string path, string cacheKey);
        void Remove(string objectId);
        void Clear();
        int CacheSize { get; }
    }

    /// <summary>
    /// Cache implementation that doesn't cache.
    /// </summary>
    public class NoCache : ICache
    {
        public void Initialize(ISession session, IDictionary<string, string> parameters) { }
        public bool ContainsId(string objectId, string cacheKey) { return false; }
        public bool ContainsPath(string path, string cacheKey) { return false; }
        public void Put(ICmisObject cmisObject, string cacheKey) { }
        public void PutPath(string path, ICmisObject cmisObject, string cacheKey) { }
        public ICmisObject GetById(string objectId, string cacheKey) { return null; }
        public ICmisObject GetByPath(string path, string cacheKey) { return null; }
        public void Remove(string objectId) { }
        public void Clear() { }
        public int CacheSize { get { return 0; } }
    }

    public class CmisObjectCache : ICache
    {
        private int cacheSize;
        private int cacheTtl;
        private int pathToIdSize;
        private int pathToIdTtl;

        private LRUCache<string, IDictionary<string, ICmisObject>> objectCache;
        private LRUCache<string, string> pathToIdCache;

        private object cacheLock = new object();

        public CmisObjectCache() { }

        public void Initialize(ISession session, IDictionary<string, string> parameters)
        {
            Lock();
            try
            {
                // cache size
                cacheSize = 1000;
                try
                {
                    string cacheSizeStr;
                    if (parameters.TryGetValue(SessionParameter.CacheSizeObjects, out cacheSizeStr))
                    {
                        cacheSize = Int32.Parse(cacheSizeStr);
                        if (cacheSize < 0)
                        {
                            cacheSize = 0;
                        }
                    }
                }
                catch (Exception) { }

                // cache time-to-live
                cacheTtl = 2 * 60 * 60 * 1000;
                try
                {
                    string cacheTtlStr;
                    if (parameters.TryGetValue(SessionParameter.CacheTTLObjects, out cacheTtlStr))
                    {
                        cacheTtl = Int32.Parse(cacheTtlStr);
                        if (cacheTtl < 0)
                        {
                            cacheTtl = 2 * 60 * 60 * 1000;
                        }
                    }
                }
                catch (Exception) { }

                // path-to-id size
                pathToIdSize = 1000;
                try
                {
                    string pathToIdSizeStr;
                    if (parameters.TryGetValue(SessionParameter.CacheSizePathToId, out pathToIdSizeStr))
                    {
                        pathToIdSize = Int32.Parse(pathToIdSizeStr);
                        if (pathToIdSize < 0)
                        {
                            pathToIdSize = 0;
                        }
                    }
                }
                catch (Exception) { }

                // path-to-id time-to-live
                pathToIdTtl = 30 * 60 * 1000;
                try
                {
                    string pathToIdTtlStr;
                    if (parameters.TryGetValue(SessionParameter.CacheTTLPathToId, out pathToIdTtlStr))
                    {
                        pathToIdTtl = Int32.Parse(pathToIdTtlStr);
                        if (pathToIdTtl < 0)
                        {
                            pathToIdTtl = 30 * 60 * 1000;
                        }
                    }
                }
                catch (Exception) { }

                InitializeInternals();
            }
            finally
            {
                Unlock();
            }
        }

        private void InitializeInternals()
        {
            Lock();
            try
            {
                objectCache = new LRUCache<string, IDictionary<string, ICmisObject>>(cacheSize, TimeSpan.FromMilliseconds(cacheTtl));
                pathToIdCache = new LRUCache<string, string>(pathToIdSize, TimeSpan.FromMilliseconds(pathToIdTtl));
            }
            finally
            {
                Unlock();
            }
        }

        public void Clear()
        {
            InitializeInternals();
        }

        public bool ContainsId(string objectId, string cacheKey)
        {
            Lock();
            try
            {
                return objectCache.Get(objectId) != null;
            }
            finally
            {
                Unlock();
            }
        }

        public bool ContainsPath(string path, string cacheKey)
        {
            Lock();
            try
            {
                return pathToIdCache.Get(path) != null;
            }
            finally
            {
                Unlock();
            }
        }

        public ICmisObject GetById(string objectId, string cacheKey)
        {
            Lock();
            try
            {
                IDictionary<string, ICmisObject> cacheKeyDict = objectCache.Get(objectId);
                if (cacheKeyDict == null)
                {
                    return null;
                }

                ICmisObject cmisObject;
                if (cacheKeyDict.TryGetValue(cacheKey, out cmisObject))
                {
                    return cmisObject;
                }

                return null;
            }
            finally
            {
                Unlock();
            }
        }

        public ICmisObject GetByPath(string path, string cacheKey)
        {
            Lock();
            try
            {
                string id = pathToIdCache.Get(path);
                if (id == null)
                {
                    return null;
                }

                return GetById(id, cacheKey);
            }
            finally
            {
                Unlock();
            }
        }

        public void Put(ICmisObject cmisObject, string cacheKey)
        {
            // no object, no id, no cache key - no cache
            if (cmisObject == null || cmisObject.Id == null || cacheKey == null)
            {
                return;
            }

            Lock();
            try
            {
                IDictionary<string, ICmisObject> cacheKeyDict = objectCache.Get(cmisObject.Id);
                if (cacheKeyDict == null)
                {
                    cacheKeyDict = new Dictionary<string, ICmisObject>();
                    objectCache.Add(cmisObject.Id, cacheKeyDict);
                }

                cacheKeyDict[cacheKey] = cmisObject;

                // folders may have a path, use it!
                string path = cmisObject.GetPropertyValue(PropertyIds.Path) as string;
                if (path != null)
                {
                    pathToIdCache.Add(path, cmisObject.Id);
                }
            }
            finally
            {
                Unlock();
            }
        }

        public void PutPath(string path, ICmisObject cmisObject, string cacheKey)
        {
            // no path, no object, no id, no cache key - no cache
            if (path == null || cmisObject == null || cmisObject.Id == null || cacheKey == null)
            {
                return;
            }

            Lock();
            try
            {
                Put(cmisObject, cacheKey);
                pathToIdCache.Add(path, cmisObject.Id);
            }
            finally
            {
                Unlock();
            }
        }

        public void Remove(string objectId)
        {
            if (objectId == null)
            {
                return;
            }

            Lock();
            try
            {
                objectCache.Remove(objectId);
            }
            finally
            {
                Unlock();
            }
        }

        public int CacheSize
        {
            get { return cacheSize; }
        }

        protected void Lock()
        {
            Monitor.Enter(cacheLock);
        }

        protected void Unlock()
        {
            Monitor.Exit(cacheLock);
        }
    }
}
