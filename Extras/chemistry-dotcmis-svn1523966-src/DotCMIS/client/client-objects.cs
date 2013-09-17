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
using System.Text;
using System.Threading;
using DotCMIS.Binding;
using DotCMIS.Binding.Services;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMIS.Client.Impl
{
    /// <summary>
    /// CMIS object base class.
    /// </summary>
    public abstract class AbstractCmisObject : ICmisObject
    {
        protected ISession Session { get; private set; }
        protected string RepositoryId { get { return Session.RepositoryInfo.Id; } }
        protected ICmisBinding Binding { get { return Session.Binding; } }

        private IObjectType objectType;
        public IObjectType ObjectType
        {
            get
            {
                lock (objectLock)
                {
                    return objectType;
                }
            }
        }

        protected string ObjectId
        {
            get
            {
                string objectId = Id;
                if (objectId == null)
                {
                    throw new CmisRuntimeException("Object Id is unknown!");
                }

                return objectId;
            }
        }

        protected IOperationContext CreationContext { get; private set; }

        private IDictionary<string, IProperty> properties;
        private IAllowableActions allowableActions;
        private IList<IRendition> renditions;
        private IAcl acl;
        private IList<IPolicy> policies;
        private IList<IRelationship> relationships;
        private IDictionary<ExtensionLevel, IList<ICmisExtensionElement>> extensions;

        protected object objectLock = new object();

        protected void Initialize(ISession session, IObjectType objectType, IObjectData objectData, IOperationContext context)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            if (objectType == null)
            {
                throw new ArgumentNullException("objectType");
            }

            if (objectType.PropertyDefinitions == null || objectType.PropertyDefinitions.Count < 9)
            {
                // there must be at least the 9 standard properties that all objects have
                throw new ArgumentException("Object type must have property definitions!");
            }

            this.Session = session;
            this.objectType = objectType;
            this.extensions = new Dictionary<ExtensionLevel, IList<ICmisExtensionElement>>();
            this.CreationContext = new OperationContext(context);
            this.RefreshTimestamp = DateTime.UtcNow;

            IObjectFactory of = Session.ObjectFactory;

            if (objectData != null)
            {
                // handle properties
                if (objectData.Properties != null)
                {
                    properties = of.ConvertProperties(objectType, objectData.Properties);
                    extensions[ExtensionLevel.Properties] = objectData.Properties.Extensions;
                }

                // handle allowable actions
                if (objectData.AllowableActions != null)
                {
                    allowableActions = objectData.AllowableActions;
                    extensions[ExtensionLevel.AllowableActions] = objectData.AllowableActions.Extensions;
                }

                // handle renditions
                if (objectData.Renditions != null)
                {
                    renditions = new List<IRendition>();
                    foreach (IRenditionData rd in objectData.Renditions)
                    {
                        renditions.Add(of.ConvertRendition(Id, rd));
                    }
                }

                // handle ACL
                if (objectData.Acl != null)
                {
                    acl = objectData.Acl;
                    extensions[ExtensionLevel.Acl] = objectData.Acl.Extensions;
                }

                // handle policies
                if (objectData.PolicyIds != null && objectData.PolicyIds.PolicyIds != null)
                {
                    policies = new List<IPolicy>();
                    foreach (string pid in objectData.PolicyIds.PolicyIds)
                    {
                        IPolicy policy = Session.GetObject(Session.CreateObjectId(pid)) as IPolicy;
                        if (policy != null)
                        {
                            policies.Add(policy);
                        }
                    }
                    extensions[ExtensionLevel.Policies] = objectData.PolicyIds.Extensions;
                }

                // handle relationships
                if (objectData.Relationships != null)
                {
                    relationships = new List<IRelationship>();
                    foreach (IObjectData rod in objectData.Relationships)
                    {
                        IRelationship relationship = of.ConvertObject(rod, CreationContext) as IRelationship;
                        if (relationship != null)
                        {
                            relationships.Add(relationship);
                        }
                    }
                }

                extensions[ExtensionLevel.Object] = objectData.Extensions;
            }
        }

        protected string GetPropertyQueryName(string propertyId)
        {
            lock (objectLock)
            {
                IPropertyDefinition propDef = objectType[propertyId];
                if (propDef == null)
                {
                    return null;
                }

                return propDef.QueryName;
            }
        }

        // --- object ---

        public void Delete(bool allVersions)
        {
            lock (objectLock)
            {
                Session.Delete(this, allVersions);
            }
        }

        public ICmisObject UpdateProperties(IDictionary<string, object> properties)
        {
            IObjectId objectId = UpdateProperties(properties, true);
            if (objectId == null)
            {
                return null;
            }

            if (ObjectId != objectId.Id)
            {
                return Session.GetObject(objectId, CreationContext);
            }

            return this;
        }

        public IObjectId UpdateProperties(IDictionary<String, object> properties, bool refresh)
        {
            if (properties == null || properties.Count == 0)
            {
                throw new ArgumentException("Properties must not be empty!");
            }

            string newObjectId = null;

            lock (objectLock)
            {
                string objectId = ObjectId;
                string changeToken = ChangeToken;

                HashSet<Updatability> updatebility = new HashSet<Updatability>();
                updatebility.Add(Updatability.ReadWrite);

                // check if checked out
                bool? isCheckedOut = GetPropertyValue(PropertyIds.IsVersionSeriesCheckedOut) as bool?;
                if (isCheckedOut.HasValue && isCheckedOut.Value)
                {
                    updatebility.Add(Updatability.WhenCheckedOut);
                }

                // it's time to update
                Binding.GetObjectService().UpdateProperties(RepositoryId, ref objectId, ref changeToken,
                        Session.ObjectFactory.ConvertProperties(properties, this.objectType, updatebility), null);

                newObjectId = objectId;
            }

            if (refresh)
            {
                Refresh();
            }

            if (newObjectId == null)
            {
                return null;
            }

            return Session.CreateObjectId(newObjectId);
        }

        // --- properties ---

        public IObjectType BaseType { get { return Session.GetTypeDefinition(GetPropertyValue(PropertyIds.BaseTypeId) as string); } }

        public BaseTypeId BaseTypeId
        {
            get
            {
                string baseType = GetPropertyValue(PropertyIds.BaseTypeId) as string;
                if (baseType == null) { throw new CmisRuntimeException("Base type not set!"); }

                return baseType.GetCmisEnum<BaseTypeId>();
            }
        }

        public string Id { get { return GetPropertyValue(PropertyIds.ObjectId) as string; } }

        public string Name { get { return GetPropertyValue(PropertyIds.Name) as string; } }

        public string CreatedBy { get { return GetPropertyValue(PropertyIds.CreatedBy) as string; } }

        public DateTime? CreationDate { get { return GetPropertyValue(PropertyIds.CreationDate) as DateTime?; } }

        public string LastModifiedBy { get { return GetPropertyValue(PropertyIds.LastModifiedBy) as string; } }

        public DateTime? LastModificationDate { get { return GetPropertyValue(PropertyIds.LastModificationDate) as DateTime?; } }

        public string ChangeToken { get { return GetPropertyValue(PropertyIds.ChangeToken) as string; } }

        public IList<IProperty> Properties
        {
            get
            {
                lock (objectLock)
                {
                    return new List<IProperty>(properties.Values);
                }
            }
        }

        public IProperty this[string propertyId]
        {
            get
            {
                if (propertyId == null)
                {
                    throw new ArgumentNullException("propertyId");
                }

                lock (objectLock)
                {
                    IProperty property;
                    if (properties.TryGetValue(propertyId, out property))
                    {
                        return property;
                    }
                    return null;
                }
            }
        }

        public object GetPropertyValue(string propertyId)
        {
            IProperty property = this[propertyId];
            if (property == null) { return null; }

            return property.Value;
        }

        // --- allowable actions ---

        public IAllowableActions AllowableActions
        {
            get
            {
                lock (objectLock)
                {
                    return allowableActions;
                }
            }
        }

        // --- renditions ---

        public IList<IRendition> Renditions
        {
            get
            {
                lock (objectLock)
                {
                    return renditions;
                }
            }
        }

        // --- ACL ---

        public IAcl getAcl(bool onlyBasicPermissions)
        {
            return Binding.GetAclService().GetAcl(RepositoryId, ObjectId, onlyBasicPermissions, null);
        }

        public IAcl ApplyAcl(IList<IAce> addAces, IList<IAce> removeAces, AclPropagation? aclPropagation)
        {
            IAcl result = Session.ApplyAcl(this, addAces, removeAces, aclPropagation);

            Refresh();

            return result;
        }

        public IAcl AddAcl(IList<IAce> addAces, AclPropagation? aclPropagation)
        {
            return ApplyAcl(addAces, null, aclPropagation);
        }

        public IAcl RemoveAcl(IList<IAce> removeAces, AclPropagation? aclPropagation)
        {
            return ApplyAcl(null, removeAces, aclPropagation);
        }

        public IAcl Acl
        {
            get
            {
                lock (objectLock)
                {
                    return acl;
                }
            }
        }

        // --- policies ---

        public void ApplyPolicy(params IObjectId[] policyId)
        {
            lock (objectLock)
            {
                Session.ApplyPolicy(this, policyId);
            }

            Refresh();
        }

        public void RemovePolicy(params IObjectId[] policyId)
        {
            lock (objectLock)
            {
                Session.RemovePolicy(this, policyId);
            }

            Refresh();
        }

        public IList<IPolicy> Policies
        {
            get
            {
                lock (objectLock)
                {
                    return policies;
                }
            }
        }

        // --- relationships ---

        public IList<IRelationship> Relationships
        {
            get
            {
                lock (objectLock)
                {
                    return relationships;
                }
            }
        }

        // --- extensions ---

        public IList<ICmisExtensionElement> GetExtensions(ExtensionLevel level)
        {
            IList<ICmisExtensionElement> ext;
            if (extensions.TryGetValue(level, out ext))
            {
                return ext;
            }

            return null;
        }

        // --- other ---

        public DateTime RefreshTimestamp { get; private set; }

        public void Refresh()
        {
            lock (objectLock)
            {
                IOperationContext oc = CreationContext;

                // get the latest data from the repository
                IObjectData objectData = Binding.GetObjectService().GetObject(RepositoryId, ObjectId, oc.FilterString, oc.IncludeAllowableActions,
                    oc.IncludeRelationships, oc.RenditionFilterString, oc.IncludePolicies, oc.IncludeAcls, null);

                // reset this object
                Initialize(Session, ObjectType, objectData, CreationContext);
            }
        }

        public void RefreshIfOld(long durationInMillis)
        {
            lock (objectLock)
            {
                if (((DateTime.UtcNow - RefreshTimestamp).Ticks / 10000) > durationInMillis)
                {
                    Refresh();
                }
            }
        }
    }

    /// <summary>
    /// Fileable object base class.
    /// </summary>
    public abstract class AbstractFileableCmisObject : AbstractCmisObject, IFileableCmisObject
    {
        public IFileableCmisObject Move(IObjectId sourceFolderId, IObjectId targetFolderId)
        {
            string objectId = ObjectId;

            if (sourceFolderId == null || sourceFolderId.Id == null)
            {
                throw new ArgumentException("Source folder id must be set!");
            }

            if (targetFolderId == null || targetFolderId.Id == null)
            {
                throw new ArgumentException("Target folder id must be set!");
            }

            Binding.GetObjectService().MoveObject(RepositoryId, ref objectId, targetFolderId.Id, sourceFolderId.Id, null);

            if (objectId == null)
            {
                return null;
            }

            IFileableCmisObject movedObject = Session.GetObject(Session.CreateObjectId(objectId)) as IFileableCmisObject;
            if (movedObject == null)
            {
                throw new CmisRuntimeException("Moved object is invalid!");
            }

            return movedObject;
        }

        public virtual IList<IFolder> Parents
        {
            get
            {
                // get object ids of the parent folders
                IList<IObjectParentData> bindingParents = Binding.GetNavigationService().GetObjectParents(RepositoryId, ObjectId,
                    GetPropertyQueryName(PropertyIds.ObjectId), false, IncludeRelationshipsFlag.None, null, false, null);

                IList<IFolder> parents = new List<IFolder>();

                foreach (IObjectParentData p in bindingParents)
                {
                    if (p == null || p.Object == null || p.Object.Properties == null)
                    {
                        // should not happen...
                        throw new CmisRuntimeException("Repository sent invalid data!");
                    }

                    // get id property
                    IPropertyData idProperty = p.Object.Properties[PropertyIds.ObjectId];
                    if (idProperty == null || idProperty.PropertyType != PropertyType.Id)
                    {
                        // the repository sent an object without a valid object id...
                        throw new CmisRuntimeException("Repository sent invalid data! No object id!");
                    }

                    // fetch the object and make sure it is a folder
                    IObjectId parentId = Session.CreateObjectId(idProperty.FirstValue as string);
                    IFolder parentFolder = Session.GetObject(parentId) as IFolder;
                    if (parentFolder == null)
                    {
                        // the repository sent an object that is not a folder...
                        throw new CmisRuntimeException("Repository sent invalid data! Object is not a folder!");
                    }

                    parents.Add(parentFolder);
                }

                return parents;
            }
        }

        public virtual IList<string> Paths
        {
            get
            {
                // get object paths of the parent folders
                IList<IObjectParentData> parents = Binding.GetNavigationService().GetObjectParents(
                        RepositoryId, ObjectId, GetPropertyQueryName(PropertyIds.Path), false, IncludeRelationshipsFlag.None,
                        null, true, null);

                IList<string> paths = new List<string>();

                foreach (IObjectParentData p in parents)
                {
                    if (p == null || p.Object == null || p.Object.Properties == null)
                    {
                        // should not happen...
                        throw new CmisRuntimeException("Repository sent invalid data!");
                    }

                    // get path property
                    IPropertyData pathProperty = p.Object.Properties[PropertyIds.Path];
                    if (pathProperty == null || pathProperty.PropertyType != PropertyType.String)
                    {
                        // the repository sent a folder without a valid path...
                        throw new CmisRuntimeException("Repository sent invalid data! No path property!");
                    }

                    if (p.RelativePathSegment == null)
                    {
                        // the repository didn't send a relative path segment
                        throw new CmisRuntimeException("Repository sent invalid data! No relative path segement!");
                    }

                    string folderPath = pathProperty.FirstValue as string;
                    paths.Add(folderPath + (folderPath.EndsWith("/") ? "" : "/") + p.RelativePathSegment);
                }

                return paths;
            }
        }

        public void AddToFolder(IObjectId folderId, bool allVersions)
        {
            if (folderId == null || folderId.Id == null)
            {
                throw new ArgumentException("Folder Id must be set!");
            }

            Binding.GetMultiFilingService().AddObjectToFolder(RepositoryId, ObjectId, folderId.Id, allVersions, null);
        }

        public void RemoveFromFolder(IObjectId folderId)
        {
            Binding.GetMultiFilingService().RemoveObjectFromFolder(RepositoryId, ObjectId, folderId == null ? null : folderId.Id, null);
        }
    }

    /// <summary>
    /// Document implemetation.
    /// </summary>
    public class Document : AbstractFileableCmisObject, IDocument
    {
        public Document(ISession session, IObjectType objectType, IObjectData objectData, IOperationContext context)
        {
            Initialize(session, objectType, objectData, context);
        }

        // properties

        public bool? IsImmutable { get { return GetPropertyValue(PropertyIds.IsImmutable) as bool?; } }

        public bool? IsLatestVersion { get { return GetPropertyValue(PropertyIds.IsLatestVersion) as bool?; } }

        public bool? IsMajorVersion { get { return GetPropertyValue(PropertyIds.IsMajorVersion) as bool?; } }

        public bool? IsLatestMajorVersion { get { return GetPropertyValue(PropertyIds.IsLatestMajorVersion) as bool?; } }

        public string VersionLabel { get { return GetPropertyValue(PropertyIds.VersionLabel) as string; } }

        public string VersionSeriesId { get { return GetPropertyValue(PropertyIds.VersionSeriesId) as string; } }

        public bool? IsVersionSeriesCheckedOut { get { return GetPropertyValue(PropertyIds.IsVersionSeriesCheckedOut) as bool?; } }

        public string VersionSeriesCheckedOutBy { get { return GetPropertyValue(PropertyIds.VersionSeriesCheckedOutBy) as string; } }

        public string VersionSeriesCheckedOutId { get { return GetPropertyValue(PropertyIds.VersionSeriesCheckedOutId) as string; } }

        public string CheckinComment { get { return GetPropertyValue(PropertyIds.CheckinComment) as string; } }

        public long? ContentStreamLength { get { return GetPropertyValue(PropertyIds.ContentStreamLength) as long?; } }

        public string ContentStreamMimeType { get { return GetPropertyValue(PropertyIds.ContentStreamMimeType) as string; } }

        public string ContentStreamFileName { get { return GetPropertyValue(PropertyIds.ContentStreamFileName) as string; } }

        public string ContentStreamId { get { return GetPropertyValue(PropertyIds.ContentStreamId) as string; } }

        // operations

        public IDocument Copy(IObjectId targetFolderId, IDictionary<string, object> properties, VersioningState? versioningState,
                IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context)
        {

            IObjectId newId = Session.CreateDocumentFromSource(this, properties, targetFolderId, versioningState, policies, addAces, removeAces);

            // if no context is provided the object will not be fetched
            if (context == null || newId == null)
            {
                return null;
            }
            // get the new object
            IDocument newDoc = Session.GetObject(newId, context) as IDocument;
            if (newDoc == null)
            {
                throw new CmisRuntimeException("Newly created object is not a document! New id: " + newId);
            }

            return newDoc;
        }

        public IDocument Copy(IObjectId targetFolderId)
        {
            return Copy(targetFolderId, null, null, null, null, null, Session.DefaultContext);
        }

        public void DeleteAllVersions()
        {
            Delete(true);
        }

        // versioning

        public IObjectId CheckOut()
        {
            string newObjectId = null;

            lock (objectLock)
            {
                string objectId = ObjectId;
                bool? contentCopied;

                Binding.GetVersioningService().CheckOut(RepositoryId, ref objectId, null, out contentCopied);
                newObjectId = objectId;
            }

            if (newObjectId == null)
            {
                return null;
            }

            return Session.CreateObjectId(newObjectId);
        }

        public void CancelCheckOut()
        {
            Binding.GetVersioningService().CancelCheckOut(RepositoryId, ObjectId, null);
        }

        public IObjectId CheckIn(bool major, IDictionary<string, object> properties, IContentStream contentStream,
                string checkinComment, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces)
        {
            String newObjectId = null;

            lock (objectLock)
            {
                string objectId = ObjectId;

                IObjectFactory of = Session.ObjectFactory;

                HashSet<Updatability> updatebility = new HashSet<Updatability>();
                updatebility.Add(Updatability.ReadWrite);
                updatebility.Add(Updatability.WhenCheckedOut);

                Binding.GetVersioningService().CheckIn(RepositoryId, ref objectId, major, of.ConvertProperties(properties, ObjectType, updatebility),
                    contentStream, checkinComment, of.ConvertPolicies(policies), of.ConvertAces(addAces), of.ConvertAces(removeAces), null);

                newObjectId = objectId;
            }

            if (newObjectId == null)
            {
                return null;
            }

            return Session.CreateObjectId(newObjectId);

        }

        public IList<IDocument> GetAllVersions()
        {
            return GetAllVersions(Session.DefaultContext);
        }

        public IList<IDocument> GetAllVersions(IOperationContext context)
        {
            string objectId;
            string versionSeriesId;

            lock (objectLock)
            {
                objectId = ObjectId;
                versionSeriesId = VersionSeriesId;
            }

            IList<IObjectData> versions = Binding.GetVersioningService().GetAllVersions(RepositoryId, objectId, versionSeriesId,
                context.FilterString, context.IncludeAllowableActions, null);

            IObjectFactory of = Session.ObjectFactory;

            IList<IDocument> result = new List<IDocument>();
            if (versions != null)
            {
                foreach (IObjectData objectData in versions)
                {
                    IDocument doc = of.ConvertObject(objectData, context) as IDocument;
                    if (doc == null)
                    {
                        // should not happen...
                        continue;
                    }

                    result.Add(doc);
                }
            }

            return result;
        }

        public IDocument GetObjectOfLatestVersion(bool major)
        {
            return GetObjectOfLatestVersion(major, Session.DefaultContext);
        }

        public IDocument GetObjectOfLatestVersion(bool major, IOperationContext context)
        {
            string objectId;
            string versionSeriesId;

            lock (objectLock)
            {
                objectId = ObjectId;
                versionSeriesId = VersionSeriesId;
            }

            if (versionSeriesId == null)
            {
                throw new CmisRuntimeException("Version series id is unknown!");
            }

            IObjectData objectData = Binding.GetVersioningService().GetObjectOfLatestVersion(RepositoryId, objectId, versionSeriesId, major,
                context.FilterString, context.IncludeAllowableActions, context.IncludeRelationships, context.RenditionFilterString,
                context.IncludePolicies, context.IncludeAcls, null);

            IDocument result = Session.ObjectFactory.ConvertObject(objectData, context) as IDocument;
            if (result == null)
            {
                throw new CmisRuntimeException("Latest version is not a document!");
            }

            return result;
        }

        // content operations

        public IContentStream GetContentStream()
        {
            return GetContentStream(null);
        }

        public IContentStream GetContentStream(string streamId)
        {
            return GetContentStream(streamId, null, null);
        }

        public IContentStream GetContentStream(string streamId, long? offset, long? length)
        {
            IContentStream contentStream = Session.GetContentStream(this, streamId, offset, length);
            if (contentStream == null)
            {
                // no content stream
                return null;
            }

            // the AtomPub binding doesn't return a file name
            // -> get the file name from properties, if present
            if (contentStream.FileName == null && ContentStreamFileName != null)
            {
                ContentStream newContentStream = new ContentStream();
                newContentStream.FileName = ContentStreamFileName;
                newContentStream.Length = contentStream.Length;
                newContentStream.MimeType = contentStream.MimeType;
                newContentStream.Stream = contentStream.Stream;
                newContentStream.Extensions = contentStream.Extensions;

                contentStream = newContentStream;
            }

            return contentStream;
        }

        public IDocument SetContentStream(IContentStream contentStream, bool overwrite)
        {
            IObjectId objectId = SetContentStream(contentStream, overwrite, true);
            if (objectId == null)
            {
                return null;
            }

            if (ObjectId != objectId.Id)
            {
                return (IDocument)Session.GetObject(objectId, CreationContext);
            }

            return this;
        }

        public IObjectId SetContentStream(IContentStream contentStream, bool overwrite, bool refresh)
        {
            string newObjectId = null;

            lock (objectLock)
            {
                string objectId = ObjectId;
                string changeToken = ChangeToken;

                Binding.GetObjectService().SetContentStream(RepositoryId, ref objectId, overwrite, ref changeToken, contentStream, null);

                newObjectId = objectId;
            }

            if (refresh)
            {
                Refresh();
            }

            if (newObjectId == null)
            {
                return null;
            }

            return Session.CreateObjectId(newObjectId);
        }

        public IDocument DeleteContentStream()
        {
            IObjectId objectId = DeleteContentStream(true);
            if (objectId == null)
            {
                return null;
            }

            if (ObjectId != objectId.Id)
            {
                return (IDocument)Session.GetObject(objectId, CreationContext);
            }

            return this;
        }

        public IObjectId DeleteContentStream(bool refresh)
        {
            string newObjectId = null;

            lock (objectLock)
            {
                string objectId = ObjectId;
                string changeToken = ChangeToken;

                Binding.GetObjectService().DeleteContentStream(RepositoryId, ref objectId, ref changeToken, null);

                newObjectId = objectId;
            }

            if (refresh)
            {
                Refresh();
            }

            if (newObjectId == null)
            {
                return null;
            }

            return Session.CreateObjectId(newObjectId);
        }

        public IObjectId CheckIn(bool major, IDictionary<String, object> properties, IContentStream contentStream, string checkinComment)
        {
            return this.CheckIn(major, properties, contentStream, checkinComment, null, null, null);
        }
    }

    /// <summary>
    /// Folder implemetation.
    /// </summary>
    public class Folder : AbstractFileableCmisObject, IFolder
    {
        public Folder(ISession session, IObjectType objectType, IObjectData objectData, IOperationContext context)
        {
            Initialize(session, objectType, objectData, context);
        }

        public IDocument CreateDocument(IDictionary<string, object> properties, IContentStream contentStream, VersioningState? versioningState,
            IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context)
        {
            IObjectId newId = Session.CreateDocument(properties, this, contentStream, versioningState, policies, addAces, removeAces);

            // if no context is provided the object will not be fetched
            if (context == null || newId == null)
            {
                return null;
            }

            // get the new object
            IDocument newDoc = Session.GetObject(newId, context) as IDocument;
            if (newDoc == null)
            {
                throw new CmisRuntimeException("Newly created object is not a document! New id: " + newId);
            }

            return newDoc;
        }

        public IDocument CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, VersioningState? versioningState,
            IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context)
        {
            IObjectId newId = Session.CreateDocumentFromSource(source, properties, this, versioningState, policies, addAces, removeAces);

            // if no context is provided the object will not be fetched
            if (context == null || newId == null)
            {
                return null;
            }

            // get the new object
            IDocument newDoc = Session.GetObject(newId, context) as IDocument;
            if (newDoc == null)
            {
                throw new CmisRuntimeException("Newly created object is not a document! New id: " + newId);
            }

            return newDoc;
        }

        public IFolder CreateFolder(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context)
        {
            IObjectId newId = Session.CreateFolder(properties, this, policies, addAces, removeAces);

            // if no context is provided the object will not be fetched
            if (context == null || newId == null)
            {
                return null;
            }

            // get the new object
            IFolder newFolder = Session.GetObject(newId, context) as IFolder;
            if (newFolder == null)
            {
                throw new CmisRuntimeException("Newly created object is not a folder! New id: " + newId);
            }

            return newFolder;
        }

        public IPolicy CreatePolicy(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context)
        {
            IObjectId newId = Session.CreatePolicy(properties, this, policies, addAces, removeAces);

            // if no context is provided the object will not be fetched
            if (context == null || newId == null)
            {
                return null;
            }

            // get the new object
            IPolicy newPolicy = Session.GetObject(newId, context) as IPolicy;
            if (newPolicy == null)
            {
                throw new CmisRuntimeException("Newly created object is not a policy! New id: " + newId);
            }

            return newPolicy;
        }

        public IList<string> DeleteTree(bool allVersions, UnfileObject? unfile, bool continueOnFailure)
        {
            IFailedToDeleteData failed = Binding.GetObjectService().DeleteTree(RepositoryId, ObjectId, allVersions, unfile, continueOnFailure, null);
            return failed.Ids;
        }

        public string ParentId { get { return GetPropertyValue(PropertyIds.ParentId) as string; } }

        public IList<IObjectType> AllowedChildObjectTypes
        {
            get
            {
                IList<IObjectType> result = new List<IObjectType>();

                lock (objectLock)
                {
                    IList<string> otids = GetPropertyValue(PropertyIds.AllowedChildObjectTypeIds) as IList<string>;
                    if (otids == null)
                    {
                        return result;
                    }

                    foreach (string otid in otids)
                    {
                        result.Add(Session.GetTypeDefinition(otid));
                    }
                }

                return result;
            }
        }

        public IItemEnumerable<IDocument> GetCheckedOutDocs()
        {
            return GetCheckedOutDocs(Session.DefaultContext);
        }

        public IItemEnumerable<IDocument> GetCheckedOutDocs(IOperationContext context)
        {
            string objectId = ObjectId;
            INavigationService service = Binding.GetNavigationService();
            IObjectFactory of = Session.ObjectFactory;
            IOperationContext ctxt = new OperationContext(context);

            PageFetcher<IDocument>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // get checked out documents for this folder
                IObjectList checkedOutDocs = service.GetCheckedOutDocs(RepositoryId, objectId, ctxt.FilterString, ctxt.OrderBy, ctxt.IncludeAllowableActions,
                    ctxt.IncludeRelationships, ctxt.RenditionFilterString, maxNumItems, skipCount, null);

                IList<IDocument> page = new List<IDocument>();
                if (checkedOutDocs.Objects != null)
                {
                    foreach (IObjectData objectData in checkedOutDocs.Objects)
                    {
                        IDocument doc = of.ConvertObject(objectData, ctxt) as IDocument;
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

            return new CollectionEnumerable<IDocument>(new PageFetcher<IDocument>(ctxt.MaxItemsPerPage, fetchPageDelegate));
        }

        public IItemEnumerable<ICmisObject> GetChildren()
        {
            return GetChildren(Session.DefaultContext);
        }

        public IItemEnumerable<ICmisObject> GetChildren(IOperationContext context)
        {
            string objectId = ObjectId;
            INavigationService service = Binding.GetNavigationService();
            IObjectFactory of = Session.ObjectFactory;
            IOperationContext ctxt = new OperationContext(context);

            PageFetcher<ICmisObject>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                // get the children
                IObjectInFolderList children = service.GetChildren(RepositoryId, objectId, ctxt.FilterString, ctxt.OrderBy, ctxt.IncludeAllowableActions,
                    ctxt.IncludeRelationships, ctxt.RenditionFilterString, ctxt.IncludePathSegments, maxNumItems, skipCount, null);

                // convert objects
                IList<ICmisObject> page = new List<ICmisObject>();
                if (children.Objects != null)
                {
                    foreach (IObjectInFolderData objectData in children.Objects)
                    {
                        if (objectData.Object != null)
                        {
                            page.Add(of.ConvertObject(objectData.Object, ctxt));
                        }
                    }
                }

                return new PageFetcher<ICmisObject>.Page<ICmisObject>(page, children.NumItems, children.HasMoreItems);
            };

            return new CollectionEnumerable<ICmisObject>(new PageFetcher<ICmisObject>(ctxt.MaxItemsPerPage, fetchPageDelegate));
        }

        public IList<ITree<IFileableCmisObject>> GetDescendants(int depth)
        {
            return GetDescendants(depth, Session.DefaultContext);
        }

        public IList<ITree<IFileableCmisObject>> GetDescendants(int depth, IOperationContext context)
        {
            IList<IObjectInFolderContainer> bindingContainerList = Binding.GetNavigationService().GetDescendants(RepositoryId, ObjectId, depth,
                context.FilterString, context.IncludeAllowableActions, context.IncludeRelationships, context.RenditionFilterString,
                context.IncludePathSegments, null);

            return ConvertProviderContainer(bindingContainerList, context);
        }

        public IList<ITree<IFileableCmisObject>> GetFolderTree(int depth)
        {
            return GetFolderTree(depth, Session.DefaultContext);
        }

        public IList<ITree<IFileableCmisObject>> GetFolderTree(int depth, IOperationContext context)
        {
            IList<IObjectInFolderContainer> bindingContainerList = Binding.GetNavigationService().GetFolderTree(RepositoryId, ObjectId, depth,
                context.FilterString, context.IncludeAllowableActions, context.IncludeRelationships, context.RenditionFilterString,
                context.IncludePathSegments, null);

            return ConvertProviderContainer(bindingContainerList, context);
        }

        private IList<ITree<IFileableCmisObject>> ConvertProviderContainer(IList<IObjectInFolderContainer> bindingContainerList, IOperationContext context)
        {
            if (bindingContainerList == null || bindingContainerList.Count == 0)
            {
                return null;
            }

            IList<ITree<IFileableCmisObject>> result = new List<ITree<IFileableCmisObject>>();
            foreach (IObjectInFolderContainer oifc in bindingContainerList)
            {
                if (oifc.Object == null || oifc.Object.Object == null)
                {
                    // shouldn't happen ...
                    continue;
                }

                // convert the object
                IFileableCmisObject cmisObject = Session.ObjectFactory.ConvertObject(oifc.Object.Object, context) as IFileableCmisObject;
                if (cmisObject == null)
                {
                    // the repository must not return objects that are not fileable, but you never know...
                    continue;
                }

                // convert the children
                IList<ITree<IFileableCmisObject>> children = ConvertProviderContainer(oifc.Children, context);

                // add both to current container
                Tree<IFileableCmisObject> tree = new Tree<IFileableCmisObject>();
                tree.Item = cmisObject;
                tree.Children = children;

                result.Add(tree);
            }

            return result;
        }

        public bool IsRootFolder { get { return ObjectId == Session.RepositoryInfo.RootFolderId; } }

        public IFolder FolderParent
        {
            get
            {
                if (IsRootFolder)
                {
                    return null;
                }

                IList<IFolder> parents = Parents;
                if (parents == null || parents.Count == 0)
                {
                    return null;
                }

                return parents[0];
            }
        }

        public string Path
        {
            get
            {
                string path;

                lock (objectLock)
                {
                    // get the path property
                    path = GetPropertyValue(PropertyIds.Path) as string;

                    // if the path property isn't set, get it
                    if (path == null)
                    {
                        IObjectData objectData = Binding.GetObjectService().GetObject(RepositoryId, ObjectId,
                                GetPropertyQueryName(PropertyIds.Path), false, IncludeRelationshipsFlag.None, "cmis:none", false,
                                false, null);

                        if (objectData.Properties != null)
                        {
                            IPropertyData pathProperty = objectData.Properties[PropertyIds.Path];
                            if (pathProperty != null && pathProperty.PropertyType == PropertyType.String)
                            {
                                path = pathProperty.FirstValue as string;
                            }
                        }
                    }
                }

                // we still don't know the path ... it's not a CMIS compliant repository
                if (path == null)
                {
                    throw new CmisRuntimeException("Repository didn't return " + PropertyIds.Path + "!");
                }

                return path;
            }
        }

        public override IList<string> Paths
        {
            get
            {
                IList<string> result = new List<string>();
                result.Add(Path);

                return result;
            }
        }

        public IDocument CreateDocument(IDictionary<string, object> properties, IContentStream contentStream, VersioningState? versioningState)
        {
            return CreateDocument(properties, contentStream, versioningState, null, null, null, Session.DefaultContext);
        }

        public IDocument CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, VersioningState? versioningState)
        {
            return CreateDocumentFromSource(source, properties, versioningState, null, null, null, Session.DefaultContext);
        }

        public IFolder CreateFolder(IDictionary<string, object> properties)
        {
            return CreateFolder(properties, null, null, null, Session.DefaultContext);
        }

        public IPolicy CreatePolicy(IDictionary<string, object> properties)
        {
            return CreatePolicy(properties, null, null, null, Session.DefaultContext);
        }
    }

    /// <summary>
    /// Policy implemetation.
    /// </summary>
    public class Policy : AbstractFileableCmisObject, IPolicy
    {
        public Policy(ISession session, IObjectType objectType, IObjectData objectData, IOperationContext context)
        {
            Initialize(session, objectType, objectData, context);
        }

        public string PolicyText { get { return GetPropertyValue(PropertyIds.PolicyText) as string; } }
    }

    /// <summary>
    /// Relationship implemetation.
    /// </summary>
    public class Relationship : AbstractCmisObject, IRelationship
    {

        public Relationship(ISession session, IObjectType objectType, IObjectData objectData, IOperationContext context)
        {
            Initialize(session, objectType, objectData, context);
        }

        public ICmisObject GetSource()
        {
            return GetSource(Session.DefaultContext);
        }

        public ICmisObject GetSource(IOperationContext context)
        {
            lock (objectLock)
            {
                IObjectId sourceId = SourceId;
                if (sourceId == null)
                {
                    return null;
                }

                return Session.GetObject(sourceId, context);
            }
        }

        public IObjectId SourceId
        {
            get
            {
                string sourceId = GetPropertyValue(PropertyIds.SourceId) as string;
                if (sourceId == null || sourceId.Length == 0)
                {
                    return null;
                }

                return Session.CreateObjectId(sourceId);
            }
        }

        public ICmisObject GetTarget()
        {
            return GetTarget(Session.DefaultContext);
        }

        public ICmisObject GetTarget(IOperationContext context)
        {
            lock (objectLock)
            {
                IObjectId targetId = TargetId;
                if (targetId == null)
                {
                    return null;
                }

                return Session.GetObject(targetId, context);
            }
        }

        public IObjectId TargetId
        {
            get
            {
                string targetId = GetPropertyValue(PropertyIds.TargetId) as string;
                if (targetId == null || targetId.Length == 0)
                {
                    return null;
                }

                return Session.CreateObjectId(targetId);
            }
        }
    }

    public class Property : IProperty
    {
        public Property(IPropertyDefinition propertyDefinition, IList<object> values)
        {
            PropertyDefinition = propertyDefinition;
            Values = values;
        }

        public string Id { get { return PropertyDefinition.Id; } }

        public string LocalName { get { return PropertyDefinition.LocalName; } }

        public string DisplayName { get { return PropertyDefinition.DisplayName; } }

        public string QueryName { get { return PropertyDefinition.QueryName; } }

        public bool IsMultiValued { get { return PropertyDefinition.Cardinality == Cardinality.Multi; } }

        public PropertyType? PropertyType { get { return PropertyDefinition.PropertyType; } }

        public IPropertyDefinition PropertyDefinition { get; protected set; }

        public object Value
        {
            get
            {
                if (PropertyDefinition.Cardinality == Cardinality.Single)
                {
                    return Values == null || Values.Count == 0 ? null : Values[0];
                }
                else
                {
                    return Values;
                }
            }
        }

        public IList<object> Values { get; protected set; }

        public object FirstValue { get { return Values == null || Values.Count == 0 ? null : Values[0]; } }

        public string ValueAsString { get { return FormatValue(FirstValue); } }

        public string ValuesAsString
        {
            get
            {
                if (Values == null)
                {
                    return "[]";
                }
                else
                {
                    StringBuilder result = new StringBuilder();
                    foreach (object value in Values)
                    {
                        if (result.Length > 0)
                        {
                            result.Append(", ");
                        }

                        result.Append(FormatValue(value));
                    }

                    return "[" + result.ToString() + "]";
                }
            }
        }

        private string FormatValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            // for future formating

            return value.ToString();
        }
    }

    public class Rendition : RenditionData, IRendition
    {
        private ISession session;
        private string objectId;

        public Rendition(ISession session, string objectId, string streamId, string mimeType, long? length, string kind,
            string title, long? height, long? width, string renditionDocumentId)
        {
            this.session = session;
            this.objectId = objectId;

            StreamId = streamId;
            MimeType = mimeType;
            Length = length;
            Kind = kind;
            Title = title;
            Height = height;
            Width = width;
            RenditionDocumentId = renditionDocumentId;
        }

        public IDocument GetRenditionDocument()
        {
            return GetRenditionDocument(session.DefaultContext);
        }

        public IDocument GetRenditionDocument(IOperationContext context)
        {
            if (RenditionDocumentId == null)
            {
                return null;
            }

            return session.GetObject(session.CreateObjectId(RenditionDocumentId), context) as IDocument;
        }

        public IContentStream GetContentStream()
        {
            if (objectId == null || StreamId == null)
            {
                return null;
            }

            return session.Binding.GetObjectService().GetContentStream(session.RepositoryInfo.Id, objectId, StreamId, null, null, null);
        }
    }

    public class QueryResult : IQueryResult
    {
        private IDictionary<string, IPropertyData> propertiesById;
        private IDictionary<string, IPropertyData> propertiesByQueryName;

        public QueryResult(ISession session, IObjectData objectData)
        {
            if (objectData != null)
            {
                IObjectFactory of = session.ObjectFactory;

                // handle properties
                if (objectData.Properties != null)
                {
                    Properties = new List<IPropertyData>();
                    propertiesById = new Dictionary<string, IPropertyData>();
                    propertiesByQueryName = new Dictionary<string, IPropertyData>();

                    IList<IPropertyData> queryProperties = of.ConvertQueryProperties(objectData.Properties);

                    foreach (IPropertyData property in queryProperties)
                    {
                        Properties.Add(property);
                        if (property.Id != null)
                        {
                            propertiesById[property.Id] = property;
                        }
                        if (property.QueryName != null)
                        {
                            propertiesByQueryName[property.QueryName] = property;
                        }
                    }
                }

                // handle allowable actions
                AllowableActions = objectData.AllowableActions;

                // handle relationships
                if (objectData.Relationships != null)
                {
                    Relationships = new List<IRelationship>();
                    foreach (IObjectData rod in objectData.Relationships)
                    {
                        IRelationship relationship = of.ConvertObject(rod, session.DefaultContext) as IRelationship;
                        if (relationship != null)
                        {
                            Relationships.Add(relationship);
                        }
                    }
                }

                // handle renditions
                if (objectData.Renditions != null)
                {
                    Renditions = new List<IRendition>();
                    foreach (IRenditionData rd in objectData.Renditions)
                    {
                        Renditions.Add(of.ConvertRendition(null, rd));
                    }
                }
            }
        }

        public IPropertyData this[string queryName]
        {
            get
            {
                if (queryName == null)
                {
                    return null;
                }

                IPropertyData result;
                if (propertiesByQueryName.TryGetValue(queryName, out result))
                {
                    return result;
                }

                return null;
            }
        }

        public IList<IPropertyData> Properties { get; protected set; }

        public IPropertyData GetPropertyById(string propertyId)
        {
            if (propertyId == null)
            {
                return null;
            }

            IPropertyData result;
            if (propertiesById.TryGetValue(propertyId, out result))
            {
                return result;
            }

            return null;
        }

        public object GetPropertyValueByQueryName(string queryName)
        {
            IPropertyData property = this[queryName];
            if (property == null)
            {
                return null;
            }

            return property.FirstValue;
        }

        public object GetPropertyValueById(string propertyId)
        {
            IPropertyData property = GetPropertyById(propertyId);
            if (property == null)
            {
                return null;
            }

            return property.FirstValue;
        }

        public IList<object> GetPropertyMultivalueByQueryName(string queryName)
        {
            IPropertyData property = this[queryName];
            if (property == null)
            {
                return null;
            }

            return property.Values;
        }

        public IList<object> GetPropertyMultivalueById(string propertyId)
        {
            IPropertyData property = GetPropertyById(propertyId);
            if (property == null)
            {
                return null;
            }

            return property.Values;
        }

        public IAllowableActions AllowableActions { get; protected set; }

        public IList<IRelationship> Relationships { get; protected set; }

        public IList<IRendition> Renditions { get; protected set; }
    }

    public class ChangeEvent : ChangeEventInfo, IChangeEvent
    {
        public string ObjectId { get; set; }

        public IDictionary<string, IList<object>> Properties { get; set; }

        public IList<string> PolicyIds { get; set; }

        public IAcl Acl { get; set; }
    }

    public class ChangeEvents : IChangeEvents
    {
        public string LatestChangeLogToken { get; set; }

        public IList<IChangeEvent> ChangeEventList { get; set; }

        public bool? HasMoreItems { get; set; }

        public long? TotalNumItems { get; set; }
    }
}