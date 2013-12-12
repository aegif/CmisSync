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
using DotCMIS.Binding;
using DotCMIS.Binding.Services;
using DotCMIS.Client.Impl.Cache;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMIS.Client.Impl
{
    /// <summary>
    /// Session factory implementation.
    /// </summary>
    public class SessionFactory : ISessionFactory
    {
        private SessionFactory()
        {
        }

        public static SessionFactory NewInstance()
        {
            return new SessionFactory();
        }

        public ISession CreateSession(IDictionary<string, string> parameters)
        {
            return CreateSession(parameters, null, null, null);
        }

        public ISession CreateSession(IDictionary<string, string> parameters, IObjectFactory objectFactory, AbstractAuthenticationProvider authenticationProvider, ICache cache)
        {
            Session session = new Session(parameters, objectFactory, authenticationProvider, cache);
            session.Connect();

            return session;
        }

        public IList<IRepository> GetRepositories(IDictionary<string, string> parameters)
        {
            return GetRepositories(parameters, null, null, null);
        }

        public IList<IRepository> GetRepositories(IDictionary<string, string> parameters, IObjectFactory objectFactory, AbstractAuthenticationProvider authenticationProvider, ICache cache)
        {
            ICmisBinding binding = CmisBindingHelper.CreateBinding(parameters);

            IList<IRepositoryInfo> repositoryInfos = binding.GetRepositoryService().GetRepositoryInfos(null);

            IList<IRepository> result = new List<IRepository>();
            foreach (IRepositoryInfo data in repositoryInfos)
            {
                result.Add(new Repository(data, parameters, this, objectFactory, authenticationProvider, cache));
            }

            return result;
        }
    }

    /// <summary>
    /// Binding helper class.
    /// </summary>
    internal class CmisBindingHelper
    {
        public static ICmisBinding CreateBinding(IDictionary<string, string> parameters)
        {
            return CreateBinding(parameters, null);
        }

        public static ICmisBinding CreateBinding(IDictionary<string, string> parameters, AbstractAuthenticationProvider authenticationProvider)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            if (!parameters.ContainsKey(SessionParameter.BindingType))
            {
                parameters[SessionParameter.BindingType] = BindingType.Custom;
            }

            string bt = parameters[SessionParameter.BindingType];
            switch (bt)
            {
                case BindingType.AtomPub:
                    return CreateAtomPubBinding(parameters, authenticationProvider);
                case BindingType.WebServices:
                    return CreateWebServiceBinding(parameters, authenticationProvider);
                case BindingType.Custom:
                    return CreateCustomBinding(parameters, authenticationProvider);
                default:
                    throw new CmisRuntimeException("Ambiguous session parameter: " + parameters);
            }
        }

        private static ICmisBinding CreateCustomBinding(IDictionary<string, string> parameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CmisBindingFactory factory = CmisBindingFactory.NewInstance();
            ICmisBinding binding = factory.CreateCmisBinding(parameters, authenticationProvider);

            return binding;
        }

        private static ICmisBinding CreateWebServiceBinding(IDictionary<string, string> parameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CmisBindingFactory factory = CmisBindingFactory.NewInstance();
            ICmisBinding binding = factory.CreateCmisWebServicesBinding(parameters, authenticationProvider);

            return binding;
        }

        private static ICmisBinding CreateAtomPubBinding(IDictionary<string, string> parameters, AbstractAuthenticationProvider authenticationProvider)
        {
            CmisBindingFactory factory = CmisBindingFactory.NewInstance();
            ICmisBinding binding = factory.CreateCmisAtomPubBinding(parameters, authenticationProvider);

            return binding;
        }
    }

    /// <summary>
    /// Repository implementation.
    /// </summary>
    public class Repository : RepositoryInfo, IRepository
    {
        private IDictionary<string, string> parameters;
        private SessionFactory sessionFactory;
        private IObjectFactory objectFactory;
        private AbstractAuthenticationProvider authenticationProvider;
        private ICache cache;

        public Repository(IRepositoryInfo info, IDictionary<string, string> parameters, SessionFactory sessionFactory, IObjectFactory objectFactory, AbstractAuthenticationProvider authenticationProvider, ICache cache)
            : base(info)
        {
            this.parameters = new Dictionary<string, string>(parameters);
            this.parameters[SessionParameter.RepositoryId] = Id;

            this.sessionFactory = sessionFactory;
            this.objectFactory = objectFactory;
            this.authenticationProvider = authenticationProvider;
            this.cache = cache;
        }

        public ISession CreateSession()
        {
            return sessionFactory.CreateSession(parameters, objectFactory, authenticationProvider, cache);
        }
    }

    /// <summary>
    /// Session implementation.
    /// </summary>
    public class Session : ISession
    {
        private static HashSet<Updatability> CreateUpdatability = new HashSet<Updatability>();
        private static HashSet<Updatability> CreateAndCheckoutUpdatability = new HashSet<Updatability>();
        static Session()
        {
            CreateUpdatability.Add(Updatability.OnCreate);
            CreateUpdatability.Add(Updatability.ReadWrite);
            CreateAndCheckoutUpdatability.Add(Updatability.OnCreate);
            CreateAndCheckoutUpdatability.Add(Updatability.ReadWrite);
            CreateAndCheckoutUpdatability.Add(Updatability.WhenCheckedOut);
        }

        protected static IOperationContext FallbackContext = new OperationContext(null, false, true, false, IncludeRelationshipsFlag.None, null, true, null, true, 100);

        protected IDictionary<string, string> parameters;
        private object sessionLock = new object();

        public ICmisBinding Binding { get; protected set; }
        public IRepositoryInfo RepositoryInfo { get; protected set; }
        public string RepositoryId { get { return RepositoryInfo.Id; } }

        public IObjectFactory ObjectFactory { get; protected set; }
        protected AbstractAuthenticationProvider AuthenticationProvider { get; set; }
        protected ICache Cache { get; set; }
        protected bool cachePathOmit;

        private IOperationContext context = FallbackContext;
        public IOperationContext DefaultContext
        {
            get
            {
                lock (sessionLock)
                {
                    return context;
                }
            }
            set
            {
                lock (sessionLock)
                {
                    context = (value == null ? FallbackContext : value);
                }
            }
        }

        public Session(IDictionary<string, string> parameters, IObjectFactory objectFactory, AbstractAuthenticationProvider authenticationProvider, ICache cache)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            this.parameters = parameters;

            ObjectFactory = (objectFactory == null ? CreateObjectFactory() : objectFactory);
            AuthenticationProvider = authenticationProvider;
            Cache = (cache == null ? CreateCache() : cache);

            string cachePathOmitStr;
            if (parameters.TryGetValue(SessionParameter.CachePathOmit, out cachePathOmitStr))
            {
                cachePathOmit = cachePathOmitStr.ToLower() == "true";
            }
            else
            {
                cachePathOmit = false;
            }
        }

        public void Connect()
        {
            lock (sessionLock)
            {
                Binding = CmisBindingHelper.CreateBinding(parameters, AuthenticationProvider);

                string repositoryId;
                if (!parameters.TryGetValue(SessionParameter.RepositoryId, out repositoryId))
                {
                    throw new ArgumentException("Repository Id is not set!");
                }

                RepositoryInfo = Binding.GetRepositoryService().GetRepositoryInfo(repositoryId, null);
            }
        }

        protected ICache CreateCache()
        {
            try
            {
                string typeName;
                Type cacheType;

                if (parameters.TryGetValue(SessionParameter.CacheClass, out typeName))
                {
                    cacheType = Type.GetType(typeName);
                }
                else
                {
                    cacheType = typeof(CmisObjectCache);
                }

                ICache cacheObject = Activator.CreateInstance(cacheType) as ICache;
                if (cacheObject == null)
                {
                    throw new Exception("Class does not implement ICache!");
                }

                cacheObject.Initialize(this, parameters);

                return cacheObject;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Unable to create cache: " + e, e);
            }
        }

        protected IObjectFactory CreateObjectFactory()
        {
            try
            {
                string ofName;
                Type ofType;

                if (parameters.TryGetValue(SessionParameter.ObjectFactoryClass, out ofName))
                {
                    ofType = Type.GetType(ofName);
                }
                else
                {
                    ofType = typeof(ObjectFactory);
                }

                IObjectFactory ofObject = Activator.CreateInstance(ofType) as IObjectFactory;
                if (ofObject == null)
                {
                    throw new Exception("Class does not implement IObjectFactory!");
                }

                ofObject.Initialize(this, parameters);

                return ofObject;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Unable to create object factory: " + e, e);
            }
        }

        public void Clear()
        {
            lock (sessionLock)
            {
                Cache = CreateCache();
                Binding.ClearAllCaches();
            }
        }

        // session context

        public IOperationContext CreateOperationContext()
        {
            return new OperationContext();
        }

        public IOperationContext CreateOperationContext(HashSet<string> filter, bool includeAcls, bool includeAllowableActions, bool includePolicies,
            IncludeRelationshipsFlag includeRelationships, HashSet<string> renditionFilter, bool includePathSegments, string orderBy,
            bool cacheEnabled, int maxItemsPerPage)
        {
            return new OperationContext(filter, includeAcls, includeAllowableActions, includePolicies, includeRelationships, renditionFilter,
                includePathSegments, orderBy, cacheEnabled, maxItemsPerPage);
        }

        public IObjectId CreateObjectId(string id)
        {
            return new ObjectId(id);
        }

        // types

        public IObjectType GetTypeDefinition(string typeId)
        {
            ITypeDefinition typeDefinition = Binding.GetRepositoryService().GetTypeDefinition(RepositoryId, typeId, null);
            return ObjectFactory.ConvertTypeDefinition(typeDefinition);
        }

        public IItemEnumerable<IObjectType> GetTypeChildren(string typeId, bool includePropertyDefinitions)
        {
            IRepositoryService service = Binding.GetRepositoryService();

            PageFetcher<IObjectType>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // fetch the data
                ITypeDefinitionList tdl = service.GetTypeChildren(RepositoryId, typeId, includePropertyDefinitions, maxNumItems, skipCount, null);

                // convert type definitions
                int count = (tdl != null && tdl.List != null ? tdl.List.Count : 0);
                IList<IObjectType> page = new List<IObjectType>(count);
                if (count > 0)
                {
                    foreach (ITypeDefinition typeDefinition in tdl.List)
                    {
                        page.Add(ObjectFactory.ConvertTypeDefinition(typeDefinition));
                    }
                }

                return new PageFetcher<IObjectType>.Page<IObjectType>(page, tdl.NumItems, tdl.HasMoreItems);
            };

            return new CollectionEnumerable<IObjectType>(new PageFetcher<IObjectType>(DefaultContext.MaxItemsPerPage, fetchPageDelegate));
        }

        public IList<ITree<IObjectType>> GetTypeDescendants(string typeId, int depth, bool includePropertyDefinitions)
        {
            IList<ITypeDefinitionContainer> descendants = Binding.GetRepositoryService().GetTypeDescendants(
            RepositoryId, typeId, depth, includePropertyDefinitions, null);

            return ConvertTypeDescendants(descendants);
        }

        private IList<ITree<IObjectType>> ConvertTypeDescendants(IList<ITypeDefinitionContainer> descendantsList)
        {
            if (descendantsList == null || descendantsList.Count == 0)
            {
                return null;
            }

            IList<ITree<IObjectType>> result = new List<ITree<IObjectType>>();

            foreach (ITypeDefinitionContainer container in descendantsList)
            {
                Tree<IObjectType> tree = new Tree<IObjectType>();
                tree.Item = ObjectFactory.ConvertTypeDefinition(container.TypeDefinition);
                tree.Children = ConvertTypeDescendants(container.Children);

                result.Add(tree);
            }

            return result;
        }

        // navigation

        public IFolder GetRootFolder()
        {
            return GetRootFolder(DefaultContext);
        }

        public IFolder GetRootFolder(IOperationContext context)
        {
            IFolder rootFolder = GetObject(CreateObjectId(RepositoryInfo.RootFolderId), context) as IFolder;
            if (rootFolder == null)
            {
                throw new CmisRuntimeException("Root folder object is not a folder!");
            }

            return rootFolder;
        }

        public IItemEnumerable<IDocument> GetCheckedOutDocs()
        {
            return GetCheckedOutDocs(DefaultContext);
        }

        public IItemEnumerable<IDocument> GetCheckedOutDocs(IOperationContext context)
        {
            INavigationService service = Binding.GetNavigationService();
            IOperationContext ctxt = new OperationContext(context);

            PageFetcher<IDocument>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // get all checked out documents
                IObjectList checkedOutDocs = service.GetCheckedOutDocs(RepositoryId, null, ctxt.FilterString, ctxt.OrderBy,
                    ctxt.IncludeAllowableActions, ctxt.IncludeRelationships, ctxt.RenditionFilterString, maxNumItems, skipCount, null);

                // convert objects
                IList<IDocument> page = new List<IDocument>();
                if (checkedOutDocs.Objects != null)
                {
                    foreach (IObjectData objectData in checkedOutDocs.Objects)
                    {
                        IDocument doc = ObjectFactory.ConvertObject(objectData, ctxt) as IDocument;
                        if (doc == null)
                        {
                            // should not happen...
                            continue;
                        }

                        page.Add(doc);
                    }
                }

                return new PageFetcher<IDocument>.Page<IDocument>(page, checkedOutDocs.NumItems, checkedOutDocs.HasMoreItems);
            };

            return new CollectionEnumerable<IDocument>(new PageFetcher<IDocument>(DefaultContext.MaxItemsPerPage, fetchPageDelegate));
        }

        public ICmisObject GetObject(IObjectId objectId)
        {
            return GetObject(objectId, DefaultContext);
        }

        public ICmisObject GetObject(IObjectId objectId, IOperationContext context)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Object Id must be set!");
            }

            return GetObject(objectId.Id, context);
        }

        public ICmisObject GetObject(string objectId)
        {
            return GetObject(objectId, DefaultContext);
        }

        public ICmisObject GetObject(string objectId, IOperationContext context)
        {
            if (objectId == null)
            {
                throw new ArgumentException("Object Id must be set!");
            }
            if (context == null)
            {
                throw new ArgumentException("Operation context must be set!");
            }

            ICmisObject result = null;

            // ask the cache first
            if (context.CacheEnabled)
            {
                result = Cache.GetById(objectId, context.CacheKey);
                if (result != null)
                {
                    return result;
                }
            }

            // get the object
            IObjectData objectData = Binding.GetObjectService().GetObject(RepositoryId, objectId, context.FilterString,
                context.IncludeAllowableActions, context.IncludeRelationships, context.RenditionFilterString, context.IncludePolicies,
                context.IncludeAcls, null);

            result = ObjectFactory.ConvertObject(objectData, context);

            // put into cache
            if (context.CacheEnabled)
            {
                Cache.Put(result, context.CacheKey);
            }

            return result;
        }

        public ICmisObject GetObjectByPath(string path)
        {
            return GetObjectByPath(path, DefaultContext);
        }

        public ICmisObject GetObjectByPath(string path, IOperationContext context)
        {
            if (path == null)
            {
                throw new ArgumentException("Path must be set!");
            }
            if (context == null)
            {
                throw new ArgumentException("Operation context must be set!");
            }

            ICmisObject result = null;

            // ask the cache first
            if (context.CacheEnabled && !cachePathOmit)
            {
                result = Cache.GetByPath(path, context.CacheKey);
                if (result != null)
                {
                    return result;
                }
            }

            // get the object
            IObjectData objectData = Binding.GetObjectService().GetObjectByPath(RepositoryId, path, context.FilterString,
                context.IncludeAllowableActions, context.IncludeRelationships, context.RenditionFilterString, context.IncludePolicies,
                context.IncludeAcls, null);

            result = ObjectFactory.ConvertObject(objectData, context);

            // put into cache
            if (context.CacheEnabled)
            {
                Cache.PutPath(path, result, context.CacheKey);
            }

            return result;
        }

        public void RemoveObjectFromCache(IObjectId objectId)
        {
            if (objectId == null || objectId.Id == null)
            {
                return;
            }

            RemoveObjectFromCache(objectId.Id);
        }

        public void RemoveObjectFromCache(string objectId)
        {
            Cache.Remove(objectId);
        }

        // discovery

        public IItemEnumerable<IQueryResult> Query(string statement, bool searchAllVersions)
        {
            return Query(statement, searchAllVersions, DefaultContext);
        }

        public IItemEnumerable<IQueryResult> Query(string statement, bool searchAllVersions, IOperationContext context)
        {
            IDiscoveryService service = Binding.GetDiscoveryService();
            IOperationContext ctxt = new OperationContext(context);

            PageFetcher<IQueryResult>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // fetch the data
                IObjectList resultList = service.Query(RepositoryId, statement, searchAllVersions, ctxt.IncludeAllowableActions,
                    ctxt.IncludeRelationships, ctxt.RenditionFilterString, maxNumItems, skipCount, null);

                // convert query results
                IList<IQueryResult> page = new List<IQueryResult>();
                if (resultList.Objects != null)
                {
                    foreach (IObjectData objectData in resultList.Objects)
                    {
                        if (objectData == null)
                        {
                            continue;
                        }

                        page.Add(ObjectFactory.ConvertQueryResult(objectData));
                    }
                }

                return new PageFetcher<IQueryResult>.Page<IQueryResult>(page, resultList.NumItems, resultList.HasMoreItems);
            };

            return new CollectionEnumerable<IQueryResult>(new PageFetcher<IQueryResult>(DefaultContext.MaxItemsPerPage, fetchPageDelegate));
        }

        public IChangeEvents GetContentChanges(string changeLogToken, bool includeProperties, long maxNumItems)
        {
            return GetContentChanges(changeLogToken, includeProperties, maxNumItems, DefaultContext);
        }

        public IChangeEvents GetContentChanges(string changeLogToken, bool includeProperties, long maxNumItems,
                IOperationContext context)
        {
            lock (sessionLock)
            {
                IObjectList objectList = Binding.GetDiscoveryService().GetContentChanges(RepositoryId, ref changeLogToken, includeProperties,
                    context.FilterString, context.IncludePolicies, context.IncludeAcls, maxNumItems, null);

                return ObjectFactory.ConvertChangeEvents(changeLogToken, objectList);
            }
        }

        // create

        public IObjectId CreateDocument(IDictionary<string, object> properties, IObjectId folderId, IContentStream contentStream,
            VersioningState? versioningState, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces)
        {
            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException("Properties must not be empty!");
            }

            string newId = Binding.GetObjectService().CreateDocument(RepositoryId, ObjectFactory.ConvertProperties(properties, null,
                (versioningState == VersioningState.CheckedOut ? CreateAndCheckoutUpdatability : CreateUpdatability)),
                (folderId == null ? null : folderId.Id), contentStream, versioningState, ObjectFactory.ConvertPolicies(policies),
                ObjectFactory.ConvertAces(addAces), ObjectFactory.ConvertAces(removeAces), null);

            return newId == null ? null : CreateObjectId(newId);
        }

        public IObjectId CreateDocument(IDictionary<string, object> properties, IObjectId folderId, IContentStream contentStream,
            VersioningState? versioningState)
        {
            return CreateDocument(properties, folderId, contentStream, versioningState, null, null, null);
        }

        public IObjectId CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, IObjectId folderId,
            VersioningState? versioningState, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces)
        {
            if (source == null || source.Id == null)
            {
                throw new ArgumentException("Source must be set!");
            }

            // get the type of the source document
            IObjectType type = null;
            if (source is ICmisObject)
            {
                type = ((ICmisObject)source).ObjectType;
            }
            else
            {
                ICmisObject sourceObj = GetObject(source);
                type = sourceObj.ObjectType;
            }

            if (type.BaseTypeId != BaseTypeId.CmisDocument)
            {
                throw new ArgumentException("Source object must be a document!");
            }

            string newId = Binding.GetObjectService().CreateDocumentFromSource(RepositoryId, source.Id,
                ObjectFactory.ConvertProperties(properties, type,
                (versioningState == VersioningState.CheckedOut ? CreateAndCheckoutUpdatability : CreateUpdatability)),
                (folderId == null ? null : folderId.Id),
                versioningState, ObjectFactory.ConvertPolicies(policies), ObjectFactory.ConvertAces(addAces),
                ObjectFactory.ConvertAces(removeAces), null);

            return newId == null ? null : CreateObjectId(newId);
        }

        public IObjectId CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, IObjectId folderId,
                VersioningState? versioningState)
        {
            return CreateDocumentFromSource(source, properties, folderId, versioningState, null, null, null);
        }

        public IObjectId CreateFolder(IDictionary<string, object> properties, IObjectId folderId, IList<IPolicy> policies,
            IList<IAce> addAces, IList<IAce> removeAces)
        {
            if (folderId == null || folderId.Id == null)
            {
                throw new ArgumentException("Folder Id must be set!");
            }
            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException("Properties must not be empty!");
            }

            string newId = Binding.GetObjectService().CreateFolder(RepositoryId, ObjectFactory.ConvertProperties(properties, null, CreateUpdatability),
                (folderId == null ? null : folderId.Id), ObjectFactory.ConvertPolicies(policies), ObjectFactory.ConvertAces(addAces),
                ObjectFactory.ConvertAces(removeAces), null);

            return newId == null ? null : CreateObjectId(newId);
        }

        public IObjectId CreateFolder(IDictionary<string, object> properties, IObjectId folderId)
        {
            return CreateFolder(properties, folderId, null, null, null);
        }

        public IObjectId CreatePolicy(IDictionary<string, object> properties, IObjectId folderId, IList<IPolicy> policies,
            IList<IAce> addAces, IList<IAce> removeAces)
        {
            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException("Properties must not be empty!");
            }

            string newId = Binding.GetObjectService().CreatePolicy(RepositoryId, ObjectFactory.ConvertProperties(properties, null, CreateUpdatability),
                (folderId == null ? null : folderId.Id), ObjectFactory.ConvertPolicies(policies), ObjectFactory.ConvertAces(addAces),
                ObjectFactory.ConvertAces(removeAces), null);

            return newId == null ? null : CreateObjectId(newId);
        }

        public IObjectId CreatePolicy(IDictionary<string, object> properties, IObjectId folderId)
        {
            return CreatePolicy(properties, folderId, null, null, null);
        }

        public IObjectId CreateRelationship(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces,
                IList<IAce> removeAces)
        {
            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException("Properties must not be empty!");
            }

            string newId = Binding.GetObjectService().CreateRelationship(RepositoryId, ObjectFactory.ConvertProperties(properties, null, CreateUpdatability),
                ObjectFactory.ConvertPolicies(policies), ObjectFactory.ConvertAces(addAces), ObjectFactory.ConvertAces(removeAces), null);

            return newId == null ? null : CreateObjectId(newId);
        }

        public IObjectId CreateRelationship(IDictionary<string, object> properties)
        {
            return CreateRelationship(properties, null, null, null);
        }

        public IItemEnumerable<IRelationship> GetRelationships(IObjectId objectId, bool includeSubRelationshipTypes,
                RelationshipDirection? relationshipDirection, IObjectType type, IOperationContext context)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }

            string id = objectId.Id;
            string typeId = (type == null ? null : type.Id);
            IRelationshipService service = Binding.GetRelationshipService();
            IOperationContext ctxt = new OperationContext(context);

            PageFetcher<IRelationship>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // fetch the relationships
                IObjectList relList = service.GetObjectRelationships(RepositoryId, id, includeSubRelationshipTypes, relationshipDirection,
                    typeId, ctxt.FilterString, ctxt.IncludeAllowableActions, maxNumItems, skipCount, null);

                // convert relationship objects
                IList<IRelationship> page = new List<IRelationship>();
                if (relList.Objects != null)
                {
                    foreach (IObjectData rod in relList.Objects)
                    {
                        IRelationship relationship = GetObject(CreateObjectId(rod.Id), ctxt) as IRelationship;
                        if (relationship == null)
                        {
                            throw new CmisRuntimeException("Repository returned an object that is not a relationship!");
                        }

                        page.Add(relationship);
                    }
                }

                return new PageFetcher<IRelationship>.Page<IRelationship>(page, relList.NumItems, relList.HasMoreItems);
            };

            return new CollectionEnumerable<IRelationship>(new PageFetcher<IRelationship>(DefaultContext.MaxItemsPerPage, fetchPageDelegate));
        }

        // delete
        public void Delete(IObjectId objectId)
        {
            Delete(objectId, true);
        }

        public void Delete(IObjectId objectId, bool allVersions)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }

            Binding.GetObjectService().DeleteObject(RepositoryId, objectId.Id, allVersions, null);
            RemoveObjectFromCache(objectId);
        }

        // content stream
        public IContentStream GetContentStream(IObjectId docId)
        {
            return GetContentStream(docId, null, null, null);
        }

        public IContentStream GetContentStream(IObjectId docId, string streamId, long? offset, long? length)
        {
            if (docId == null || docId.Id == null)
            {
                throw new ArgumentException("Invalid document id!");
            }

            // get the content stream
            IContentStream contentStream = null;
            try
            {
                contentStream = Binding.GetObjectService().GetContentStream(RepositoryId, docId.Id, streamId, offset, length, null);
            }
            catch (CmisConstraintException)
            {
                // no content stream
                return null;
            }

            return contentStream;
        }

        // permissions

        public IAcl GetAcl(IObjectId objectId, bool onlyBasicPermissions)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }

            return Binding.GetAclService().GetAcl(RepositoryId, objectId.Id, onlyBasicPermissions, null);
        }

        public IAcl ApplyAcl(IObjectId objectId, IList<IAce> addAces, IList<IAce> removeAces, AclPropagation? aclPropagation)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }

            return Binding.GetAclService().ApplyAcl(RepositoryId, objectId.Id, ObjectFactory.ConvertAces(addAces),
                ObjectFactory.ConvertAces(removeAces), aclPropagation, null);
        }

        public void ApplyPolicy(IObjectId objectId, params IObjectId[] policyIds)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }
            if (policyIds == null || (policyIds.Length == 0))
            {
                throw new ArgumentException("No Policies provided!");
            }

            string[] ids = new string[policyIds.Length];
            for (int i = 0; i < policyIds.Length; i++)
            {
                if (policyIds[i] == null || policyIds[i].Id == null)
                {
                    throw new ArgumentException("A Policy Id is not set!");
                }

                ids[i] = policyIds[i].Id;
            }

            foreach (string id in ids)
            {
                Binding.GetPolicyService().ApplyPolicy(RepositoryId, id, objectId.Id, null);
            }
        }

        public void RemovePolicy(IObjectId objectId, params IObjectId[] policyIds)
        {
            if (objectId == null || objectId.Id == null)
            {
                throw new ArgumentException("Invalid object id!");
            }
            if (policyIds == null || (policyIds.Length == 0))
            {
                throw new ArgumentException("No Policies provided!");
            }

            string[] ids = new string[policyIds.Length];
            for (int i = 0; i < policyIds.Length; i++)
            {
                if (policyIds[i] == null || policyIds[i].Id == null)
                {
                    throw new ArgumentException("A Policy Id is not set!");
                }

                ids[i] = policyIds[i].Id;
            }

            foreach (string id in ids)
            {
                Binding.GetPolicyService().RemovePolicy(RepositoryId, id, objectId.Id, null);
            }
        }
    }
}
