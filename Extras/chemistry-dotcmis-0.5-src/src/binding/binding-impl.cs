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
using DotCMIS.Binding.Services;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Exceptions;

namespace DotCMIS.Binding.Impl
{
    /// <summary>
    /// Binding layer implementation.
    /// </summary>
    internal class CmisBinding : ICmisBinding
    {
        private BindingSession session;
        private BindingRepositoryService repositoryServiceWrapper;

        public CmisBinding(IDictionary<string, string> sessionParameters)
            : this(sessionParameters, null)
        {
        }

        public CmisBinding(IDictionary<string, string> sessionParameters, AbstractAuthenticationProvider authenticationProvider)
        {
            if (sessionParameters == null)
            {
                throw new ArgumentNullException("sessionParameters");
            }

            if (!sessionParameters.ContainsKey(SessionParameter.BindingSpiClass))
            {
                throw new ArgumentException("Session parameters do not contain a SPI class name!");
            }

            // initialize session
            session = new BindingSession();
            foreach (string key in sessionParameters.Keys)
            {
                session.PutValue(key, sessionParameters[key]);
            }

            // set up authentication provider
            if (authenticationProvider == null)
            {
                string authenticationProviderClass;
                if (sessionParameters.TryGetValue(SessionParameter.AuthenticationProviderClass, out authenticationProviderClass))
                {
                    try
                    {
                        Type authProvType = Type.GetType(authenticationProviderClass);
                        authenticationProvider = (AbstractAuthenticationProvider)Activator.CreateInstance(authProvType);
                        authenticationProvider.Session = session;
                        session.PutValue(BindingSession.AuthenticationProvider, authenticationProvider);
                    }
                    catch (Exception e)
                    {
                        throw new CmisRuntimeException("Could not load authentictaion provider: " + e.Message, e);
                    }
                }
            }
            else
            {
                authenticationProvider.Session = session;
                session.PutValue(BindingSession.AuthenticationProvider, authenticationProvider);
            }

            // initialize the SPI
            GetSpi();

            // set up caches
            ClearAllCaches();

            // set up repository service
            repositoryServiceWrapper = new BindingRepositoryService(session);
        }

        public IRepositoryService GetRepositoryService()
        {
            CheckSession();
            return repositoryServiceWrapper;
        }

        public INavigationService GetNavigationService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetNavigationService();
        }

        public IObjectService GetObjectService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetObjectService();
        }

        public IVersioningService GetVersioningService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetVersioningService();
        }

        public IRelationshipService GetRelationshipService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetRelationshipService();
        }

        public IDiscoveryService GetDiscoveryService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetDiscoveryService();
        }

        public IMultiFilingService GetMultiFilingService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetMultiFilingService();
        }

        public IAclService GetAclService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetAclService();
        }

        public IPolicyService GetPolicyService()
        {
            CheckSession();
            ICmisSpi spi = GetSpi();
            return spi.GetPolicyService();
        }

        public IAuthenticationProvider GetAuthenticationProvider()
        {
            return session.GetAuthenticationProvider();
        }

        public void ClearAllCaches()
        {
            CheckSession();

            session.Lock();
            try
            {
                session.PutValue(BindingSession.RepositoryInfoCache, new RepositoryInfoCache(session));
                session.PutValue(BindingSession.TypeDefinitionCache, new TypeDefinitionCache(session));

                ICmisSpi spi = GetSpi();
                spi.ClearAllCaches();
            }
            finally
            {
                session.Unlock();
            }
        }

        public void ClearRepositoryCache(string repositoryId)
        {
            CheckSession();

            if (repositoryId == null)
            {
                return;
            }

            session.Lock();
            try
            {
                RepositoryInfoCache repInfoCache = session.GetRepositoryInfoCache();
                repInfoCache.Remove(repositoryId);

                TypeDefinitionCache typeDefCache = session.GetTypeDefinitionCache();
                typeDefCache.Remove(repositoryId);

                ICmisSpi spi = GetSpi();
                spi.ClearRepositoryCache(repositoryId);
            }
            finally
            {
                session.Unlock();
            }
        }

        public void Dispose()
        {
            CheckSession();

            session.Lock();
            try
            {
                GetSpi().Dispose();
            }
            finally
            {
                session.Unlock();
                session = null;
            }
        }

        private void CheckSession()
        {
            if (session == null)
            {
                throw new ApplicationException("Already closed.");
            }
        }

        private ICmisSpi GetSpi()
        {
            return session.GetSpi();
        }
    }

    /// <summary>
    /// Session object implementation of the binding layer.
    /// </summary>
    internal class BindingSession : IBindingSession
    {
        public const string RepositoryInfoCache = "org.apache.chemistry.dotcmis.bindings.repositoryInfoCache";
        public const string TypeDefinitionCache = "org.apache.chemistry.dotcmis.bindings.typeDefintionCache";
        public const string AuthenticationProvider = "org.apache.chemistry.dotcmis.bindings.authenticationProvider";
        public const string SpiObject = "org.apache.chemistry.dotcmis.bindings.spi.object";

        private Dictionary<string, object> data;
        private object sessionLock = new object();

        public BindingSession()
        {
            data = new Dictionary<string, object>();
        }

        public object GetValue(string key)
        {
            return GetValue(key, null);
        }

        public object GetValue(string key, object defValue)
        {
            object result = null;

            Lock();
            try
            {
                if (data.TryGetValue(key, out result))
                {
                    return result;
                }
                else
                {
                    return defValue;
                }
            }
            finally
            {
                Unlock();
            }
        }

        public int GetValue(string key, int defValue)
        {
            object value = GetValue(key);

            try
            {
                if (value is string)
                {
                    return Int32.Parse((string)value);
                }
                else if (value is int)
                {
                    return (int)value;
                }
            }
            catch (Exception)
            {
            }

            return defValue;
        }

        public void PutValue(string key, object value)
        {
            Lock();
            try
            {
                data[key] = value;
            }
            finally
            {
                Unlock();
            }
        }

        public void RemoveValue(string key)
        {
            Lock();
            try
            {
                data.Remove(key);
            }
            finally
            {
                Unlock();
            }
        }

        public ICmisSpi GetSpi()
        {
            Lock();
            try
            {
                ICmisSpi spi = GetValue(SpiObject) as ICmisSpi;
                if (spi != null)
                {
                    return spi;
                }

                // ok, we have to create it...
                try
                {
                    object spiObject;
                    if (data.TryGetValue(SessionParameter.BindingSpiClass, out spiObject))
                    {
                        string spiClassName = spiObject as string;
                        if (spiClassName != null)
                        {
                            Type spiClass = Type.GetType(spiClassName);
                            spi = (ICmisSpi)Activator.CreateInstance(spiClass);
                            spi.initialize(this);
                        }
                        else
                        {
                            throw new CmisRuntimeException("SPI class is not set!");
                        }
                    }
                    else
                    {
                        throw new CmisRuntimeException("SPI class is not set!");
                    }
                }
                catch (CmisBaseException ce)
                {
                    throw ce;
                }
                catch (Exception e)
                {
                    throw new CmisRuntimeException("SPI cannot be initialized: " + e.Message, e);
                }

                // we have a SPI object -> put it into the session
                data[SpiObject] = spi;

                return spi;
            }
            finally
            {
                Unlock();
            }
        }

        public RepositoryInfoCache GetRepositoryInfoCache()
        {
            return GetValue(RepositoryInfoCache) as RepositoryInfoCache;
        }

        public TypeDefinitionCache GetTypeDefinitionCache()
        {
            return GetValue(TypeDefinitionCache) as TypeDefinitionCache;
        }

        public IAuthenticationProvider GetAuthenticationProvider()
        {
            return GetValue(AuthenticationProvider) as IAuthenticationProvider;
        }

        public void Lock()
        {
            Monitor.Enter(sessionLock);
        }

        public void Unlock()
        {
            Monitor.Exit(sessionLock);
        }

        public override string ToString()
        {
            return data.ToString();
        }
    }

    /// <summary>
    /// Repository service proxy that caches repository infos and type defintions.
    /// </summary>
    internal class BindingRepositoryService : IRepositoryService
    {
        private BindingSession session;

        public BindingRepositoryService(BindingSession session)
        {
            this.session = session;
        }

        public IList<IRepositoryInfo> GetRepositoryInfos(IExtensionsData extension)
        {
            IList<IRepositoryInfo> result = null;
            bool hasExtension = (extension != null) && (extension.Extensions != null) && (extension.Extensions.Count > 0);

            // get the SPI and fetch the repository infos
            ICmisSpi spi = session.GetSpi();
            result = spi.GetRepositoryService().GetRepositoryInfos(extension);

            // put it into the cache
            if (!hasExtension && (result != null))
            {
                RepositoryInfoCache cache = session.GetRepositoryInfoCache();
                foreach (IRepositoryInfo rid in result)
                {
                    cache.Put(rid);
                }
            }

            return result;
        }

        public IRepositoryInfo GetRepositoryInfo(string repositoryId, IExtensionsData extension)
        {
            IRepositoryInfo result = null;
            bool hasExtension = (extension != null) && (extension.Extensions != null) && (extension.Extensions.Count > 0);

            RepositoryInfoCache cache = session.GetRepositoryInfoCache();

            // if extension is not set, check the cache first
            if (!hasExtension)
            {
                result = cache.Get(repositoryId);
                if (result != null)
                {
                    return result;
                }
            }

            // it was not in the cache -> get the SPI and fetch the repository info
            ICmisSpi spi = session.GetSpi();
            result = spi.GetRepositoryService().GetRepositoryInfo(repositoryId, extension);

            // put it into the cache
            if (!hasExtension)
            {
                cache.Put(result);
            }

            return result;
        }

        public ITypeDefinitionList GetTypeChildren(string repositoryId, string typeId, bool? includePropertyDefinitions,
                long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ITypeDefinitionList result = null;
            bool hasExtension = (extension != null) && (extension.Extensions != null) && (extension.Extensions.Count > 0);

            // get the SPI and fetch the type definitions
            ICmisSpi spi = session.GetSpi();
            result = spi.GetRepositoryService().GetTypeChildren(repositoryId, typeId, includePropertyDefinitions, maxItems,
                    skipCount, extension);

            // put it into the cache
            if (!hasExtension && (includePropertyDefinitions ?? false) && (result != null) && (result.List != null))
            {
                TypeDefinitionCache cache = session.GetTypeDefinitionCache();
                foreach (ITypeDefinition tdd in result.List)
                {
                    cache.Put(repositoryId, tdd);
                }
            }

            return result;
        }

        public IList<ITypeDefinitionContainer> GetTypeDescendants(string repositoryId, string typeId, long? depth,
                bool? includePropertyDefinitions, IExtensionsData extension)
        {
            IList<ITypeDefinitionContainer> result = null;
            bool hasExtension = (extension != null) && (extension.Extensions != null) && (extension.Extensions.Count > 0);

            // get the SPI and fetch the type definitions
            ICmisSpi spi = session.GetSpi();
            result = spi.GetRepositoryService().GetTypeDescendants(repositoryId, typeId, depth, includePropertyDefinitions,
                    extension);

            // put it into the cache
            if (!hasExtension && (includePropertyDefinitions ?? false) && (result != null))
            {
                TypeDefinitionCache cache = session.GetTypeDefinitionCache();
                AddToTypeCache(cache, repositoryId, result);
            }

            return result;
        }

        private void AddToTypeCache(TypeDefinitionCache cache, string repositoryId, IList<ITypeDefinitionContainer> containers)
        {
            if (containers == null)
            {
                return;
            }

            foreach (ITypeDefinitionContainer container in containers)
            {
                cache.Put(repositoryId, container.TypeDefinition);
                AddToTypeCache(cache, repositoryId, container.Children);
            }
        }

        public ITypeDefinition GetTypeDefinition(string repositoryId, string typeId, IExtensionsData extension)
        {
            ITypeDefinition result = null;
            bool hasExtension = (extension != null) && (extension.Extensions != null) && (extension.Extensions.Count > 0);

            TypeDefinitionCache cache = session.GetTypeDefinitionCache();

            // if extension is not set, check the cache first
            if (!hasExtension)
            {
                result = cache.Get(repositoryId, typeId);
                if (result != null)
                {
                    return result;
                }
            }

            // it was not in the cache -> get the SPI and fetch the type definition
            ICmisSpi spi = session.GetSpi();
            result = spi.GetRepositoryService().GetTypeDefinition(repositoryId, typeId, extension);

            // put it into the cache
            if (!hasExtension && (result != null))
            {
                cache.Put(repositoryId, result);
            }

            return result;
        }
    }

    /// <summary>
    /// SPI interface.
    /// </summary>
    internal interface ICmisSpi : IDisposable
    {
        void initialize(BindingSession session);
        IRepositoryService GetRepositoryService();
        INavigationService GetNavigationService();
        IObjectService GetObjectService();
        IVersioningService GetVersioningService();
        IRelationshipService GetRelationshipService();
        IDiscoveryService GetDiscoveryService();
        IMultiFilingService GetMultiFilingService();
        IAclService GetAclService();
        IPolicyService GetPolicyService();
        void ClearAllCaches();
        void ClearRepositoryCache(string repositoryId);
    }
}
