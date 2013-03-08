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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using DotCMIS.Binding.Impl;
using DotCMIS.Data;
using DotCMIS.Util;

namespace DotCMIS.Binding
{
    // --- base cache implementation ---

    internal interface IBindingCache
    {
        void Initialize(string[] cacheLevelConfig);

        void Put(string[] keys, object value);

        object Get(string[] keys);

        void Remove(string[] keys);

        int Check(string[] keys);

        void Lock();

        void Unlock();
    }

    internal interface IBindingCacheLevel
    {
        void Initialize(IDictionary<string, string> cacheLevelConfig);

        object this[string key] { get; set; }

        void Remove(string key);
    }

    internal class Cache : IBindingCache
    {
        private IList<Type> cacheLevels;
        private IList<IDictionary<string, string>> cacheLevelParameters;
        private IBindingCacheLevel root;
        private string name;
        private object cacheLock = new object();

        public Cache()
            : this("Cache")
        {
        }

        public Cache(string name)
        {
            this.name = name;
        }

        public void Initialize(string[] cacheLevelConfig)
        {
            if (cacheLevels != null)
            {
                throw new ApplicationException("Cache already initialize!");
            }

            if ((cacheLevelConfig == null) || (cacheLevelConfig.Length == 0))
            {
                throw new ArgumentException("Cache config must not be empty!");
            }

            Lock();
            try
            {
                cacheLevels = new List<Type>();
                cacheLevelParameters = new List<IDictionary<string, string>>();

                // build level lists
                foreach (string config in cacheLevelConfig)
                {
                    int x = config.IndexOf(' ');
                    if (x == -1)
                    {
                        AddLevel(config, null);
                    }
                    else
                    {
                        AddLevel(config.Substring(0, x), config.Substring(x + 1));
                    }
                }

                root = CreateCacheLevel(0);
            }
            finally
            {
                Unlock();
            }
        }

        public void Put(string[] keys, object value)
        {
            if (keys == null) { return; }

            if (keys.Length != cacheLevels.Count)
            {
                throw new ArgumentException("Wrong number of keys!");
            }

            Lock();
            try
            {
                IBindingCacheLevel cacheLevel = root;

                // follow the branch
                for (int i = 0; i < keys.Length - 1; i++)
                {
                    object level = cacheLevel[keys[i]];

                    // does the branch exist?
                    if (level == null)
                    {
                        level = CreateCacheLevel(i + 1);
                        cacheLevel[keys[i]] = level;
                    }

                    // next level
                    cacheLevel = (IBindingCacheLevel)level;
                }

                cacheLevel[keys[keys.Length - 1]] = value;

                if (DotCMISDebug.DotCMISSwitch.TraceVerbose)
                {
                    Trace.WriteLine(name + ": put [" + GetFormattedKeys(keys) + "] = " + value);
                }
            }
            finally
            {
                Unlock();
            }
        }

        public object Get(string[] keys)
        {
            if (keys == null) { return null; }

            if (keys.Length != cacheLevels.Count)
            {
                throw new ArgumentException("Wrong number of keys!");
            }

            object result = null;

            Lock();
            try
            {
                IBindingCacheLevel cacheLevel = root;

                // follow the branch
                for (int i = 0; i < keys.Length - 1; i++)
                {
                    object level = cacheLevel[keys[i]];

                    // does the branch exist?
                    if (level == null) { return null; }

                    // next level
                    cacheLevel = (IBindingCacheLevel)level;
                }

                // get the value
                result = cacheLevel[keys[keys.Length - 1]];
            }
            finally
            {
                Unlock();
            }

            return result;
        }

        public void Remove(string[] keys)
        {
            if (keys == null) { return; }

            Lock();
            try
            {
                IBindingCacheLevel cacheLevel = root;

                // follow the branch
                for (int i = 0; i < keys.Length - 1; i++)
                {
                    object level = cacheLevel[keys[i]];

                    // does the branch exist?
                    if (level == null) { return; }

                    // next level
                    cacheLevel = (IBindingCacheLevel)level;
                }

                cacheLevel.Remove(keys[keys.Length - 1]);

                if (DotCMISDebug.DotCMISSwitch.TraceVerbose)
                {
                    Trace.WriteLine(name + ": removed [" + GetFormattedKeys(keys) + "]");
                }
            }
            finally
            {
                Unlock();
            }
        }

        public int Check(string[] keys)
        {
            if (keys == null) { return -1; }

            Lock();
            try
            {
                IBindingCacheLevel cacheLevel = root;

                // follow the branch
                for (int i = 0; i < keys.Length - 1; i++)
                {
                    object level = cacheLevel[keys[i]];

                    // does the branch exist?
                    if (level == null) { return i; }

                    // next level
                    cacheLevel = (IBindingCacheLevel)level;
                }

                return keys.Length;
            }
            finally
            {
                Unlock();
            }
        }

        public void Lock()
        {
            Monitor.Enter(cacheLock);
        }

        public void Unlock()
        {
            Monitor.Exit(cacheLock);
        }

        // --- internal ---

        private void AddLevel(string typeName, string parameters)
        {
            Type levelType;

            try
            {
                levelType = Type.GetType(typeName);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Class '" + typeName + "' not found!", e);
            }

            if (!typeof(IBindingCacheLevel).IsAssignableFrom(levelType))
            {
                throw new ArgumentException("Class '" + typeName + "' does not implement the ICacheLevel interface!");
            }

            cacheLevels.Add(levelType);

            // process parameters
            if (parameters == null)
            {
                cacheLevelParameters.Add(null);
            }
            else
            {
                Dictionary<string, string> parameterDict = new Dictionary<string, string>();
                cacheLevelParameters.Add(parameterDict);

                foreach (string pair in parameters.Split(','))
                {
                    string[] keyValue = pair.Split('=');
                    if (keyValue.Length == 1)
                    {
                        parameterDict[keyValue[0]] = "";
                    }
                    else
                    {
                        parameterDict[keyValue[0]] = keyValue[1];
                    }
                }
            }
        }

        private IBindingCacheLevel CreateCacheLevel(int level)
        {
            if ((level < 0) || (level >= cacheLevels.Count))
            {
                throw new ArgumentException("Cache level doesn't fit the configuration!");
            }

            // get the class and create an instance
            Type levelType = cacheLevels[level];
            IBindingCacheLevel cacheLevel = null;
            try
            {
                cacheLevel = (IBindingCacheLevel)Activator.CreateInstance(levelType);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Cache level problem?!", e);
            }

            // initialize it
            cacheLevel.Initialize(cacheLevelParameters[level]);

            return cacheLevel;
        }

        private string GetFormattedKeys(string[] keys)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string k in keys)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(k);
            }

            return sb.ToString();
        }
    }

    internal abstract class AbstractCacheLevel : IBindingCacheLevel
    {
        protected static string NullKey = "";

        private bool fallbackEnabled = false;
        private string fallbackKey = null;
        private bool singleValueEnabled = false;

        public abstract void Initialize(IDictionary<string, string> cacheLevelConfig);

        public virtual object this[string key]
        {
            get
            {
                object value = GetValue(key == null ? NullKey : key);
                if (value != null)
                {
                    return value;
                }

                if (fallbackEnabled)
                {
                    value = GetValue(fallbackKey);
                    if (value != null)
                    {
                        return value;
                    }
                }

                if (singleValueEnabled)
                {
                    value = GetSingleValue();
                    if (value != null)
                    {
                        return value;
                    }
                }

                return null;
            }
            set
            {
                if (value != null)
                {
                    AddValue(key == null ? NullKey : key, value);
                }
            }
        }

        public abstract void Remove(string key);

        protected abstract object GetValue(string key);

        protected abstract object GetSingleValue();

        protected abstract void AddValue(string key, object value);

        protected void EnableKeyFallback(string key)
        {
            fallbackKey = key;
            fallbackEnabled = true;
        }

        protected void DisableKeyFallback()
        {
            fallbackEnabled = false;
        }

        protected void EnableSingeValueFallback()
        {
            singleValueEnabled = true;
        }

        protected void DisableSingeValueFallback()
        {
            singleValueEnabled = false;
        }

        protected int GetIntParameter(IDictionary<string, string> parameters, string name, int defValue)
        {
            if (parameters == null)
            {
                return defValue;
            }

            string value;
            if (!parameters.TryGetValue(name, out value))
            {
                return defValue;
            }

            try
            {
                return Int32.Parse(value);
            }
            catch (Exception)
            {
                return defValue;
            }
        }

        protected bool GetBooleanParameter(IDictionary<string, string> parameters, string name, bool defValue)
        {
            if (parameters == null)
            {
                return defValue;
            }

            string value;
            if (!parameters.TryGetValue(name, out value))
            {
                return defValue;
            }

            try
            {
                return Boolean.Parse(value);
            }
            catch (Exception)
            {
                return defValue;
            }
        }
    }

    internal class DictionaryCacheLevel : AbstractCacheLevel
    {
        public const string Capacity = "capacity";
        public const string SingleValue = "singleValue";

        private IDictionary<string, object> dict;

        public override void Initialize(IDictionary<string, string> parameters)
        {
            int initialCapacity = GetIntParameter(parameters, Capacity, 32);
            bool singleValue = GetBooleanParameter(parameters, SingleValue, false);

            dict = new Dictionary<string, object>(initialCapacity);
            if (singleValue)
            {
                EnableSingeValueFallback();
            }
        }

        public override void Remove(string key)
        {
            dict.Remove(key);
        }

        protected override object GetValue(string key)
        {
            object value;
            if (dict.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        protected override object GetSingleValue()
        {
            if (dict.Count == 1)
            {
                return dict.Values.First();
            }

            return null;
        }

        protected override void AddValue(string key, object value)
        {
            dict[key] = value;
        }
    }

    internal class LruCacheLevel : AbstractCacheLevel
    {
        public const string MaxEntries = "maxEntries";

        private LRUCache<string, object> cache;

        public override void Initialize(IDictionary<string, string> parameters)
        {
            int maxEntries = GetIntParameter(parameters, MaxEntries, 100);

            cache = new LRUCache<string, object>(maxEntries, TimeSpan.FromDays(1));
        }

        public override void Remove(string key)
        {
            cache.Remove(key);
        }

        protected override object GetValue(string key)
        {
            return cache.Get(key);
        }

        protected override object GetSingleValue()
        {
            if (cache.Count == 1)
            {
                return cache.GetLatest();
            }

            return null;
        }

        protected override void AddValue(string key, object value)
        {
            cache.Add(key, value);
        }
    }

    // ---- Caches ----

    /// <summary>
    /// Repository Info cache.
    /// </summary>
    internal class RepositoryInfoCache
    {
        private const int CacheSizeRepositories = 10;

        private IBindingCache cache;

        public RepositoryInfoCache(BindingSession session)
        {
            int repCount = session.GetValue(SessionParameter.CacheSizeRepositories, CacheSizeRepositories);
            if (repCount < 1)
            {
                repCount = CacheSizeRepositories;
            }

            cache = new Cache("Repository Info Cache");
            cache.Initialize(new string[] { 
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount });
        }

        public void Put(IRepositoryInfo repositoryInfo)
        {
            if ((repositoryInfo == null) || (repositoryInfo.Id == null))
            {
                return;
            }

            cache.Put(new string[] { repositoryInfo.Id }, repositoryInfo);
        }

        public IRepositoryInfo Get(string repositoryId)
        {
            return (IRepositoryInfo)cache.Get(new string[] { repositoryId });
        }

        public void Remove(string repositoryId)
        {
            cache.Remove(new string[] { repositoryId });
        }
    }

    /// <summary>
    /// Type Definition cache.
    /// </summary>
    internal class TypeDefinitionCache
    {
        private const int CacheSizeRepositories = 10;
        private const int CacheSizeTypes = 100;

        private IBindingCache cache;

        public TypeDefinitionCache(BindingSession session)
        {
            int repCount = session.GetValue(SessionParameter.CacheSizeRepositories, CacheSizeRepositories);
            if (repCount < 1)
            {
                repCount = CacheSizeRepositories;
            }

            int typeCount = session.GetValue(SessionParameter.CacheSizeTypes, CacheSizeTypes);
            if (typeCount < 1)
            {
                typeCount = CacheSizeTypes;
            }

            cache = new Cache("Type Definition Cache");
            cache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(LruCacheLevel).FullName + " " + LruCacheLevel.MaxEntries + "=" + typeCount // type
        });
        }

        public void Put(string repositoryId, ITypeDefinition typeDefinition)
        {
            if ((typeDefinition == null) || (typeDefinition.Id == null))
            {
                return;
            }

            cache.Put(new string[] { repositoryId, typeDefinition.Id }, typeDefinition);
        }

        public ITypeDefinition Get(string repositoryId, string typeId)
        {
            return (ITypeDefinition)cache.Get(new string[] { repositoryId, typeId });
        }

        public void Remove(string repositoryId, string typeId)
        {
            cache.Remove(new string[] { repositoryId, typeId });
        }

        public void Remove(string repositoryId)
        {
            cache.Remove(new string[] { repositoryId });
        }
    }
}
