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
using System.Collections.Generic;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Enums;

namespace DotCMIS.Binding.Services
{
    public interface IRepositoryService
    {
        IList<IRepositoryInfo> GetRepositoryInfos(IExtensionsData extension);

        IRepositoryInfo GetRepositoryInfo(string repositoryId, IExtensionsData extension);

        ITypeDefinitionList GetTypeChildren(string repositoryId, string typeId, bool? includePropertyDefinitions,
            long? maxItems, long? skipCount, IExtensionsData extension);

        IList<ITypeDefinitionContainer> GetTypeDescendants(string repositoryId, string typeId, long? depth,
            bool? includePropertyDefinitions, IExtensionsData extension);

        ITypeDefinition GetTypeDefinition(string repositoryId, string typeId, IExtensionsData extension);
    }

    public interface INavigationService
    {
        IObjectInFolderList GetChildren(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, long? maxItems, long? skipCount, IExtensionsData extension);

        IList<IObjectInFolderContainer> GetDescendants(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension);

        IList<IObjectInFolderContainer> GetFolderTree(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension);

        IList<IObjectParentData> GetObjectParents(string repositoryId, string objectId, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includeRelativePathSegment, IExtensionsData extension);

        IObjectData GetFolderParent(string repositoryId, string folderId, string filter, ExtensionsData extension);

        IObjectList GetCheckedOutDocs(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension);
    }

    public interface IObjectService
    {
        string CreateDocument(string repositoryId, IProperties properties, string folderId, IContentStream contentStream,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension);

        string CreateDocumentFromSource(string repositoryId, string sourceId, IProperties properties, string folderId,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension);

        string CreateFolder(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension);

        string CreateRelationship(string repositoryId, IProperties properties, IList<string> policies, IAcl addAces,
            IAcl removeAces, IExtensionsData extension);

        string CreatePolicy(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension);

        IAllowableActions GetAllowableActions(string repositoryId, string objectId, IExtensionsData extension);

        IProperties GetProperties(string repositoryId, string objectId, string filter, IExtensionsData extension);

        IList<IRenditionData> GetRenditions(string repositoryId, string objectId, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension);

        IObjectData GetObject(string repositoryId, string objectId, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds,
            bool? includeAcl, IExtensionsData extension);

        IObjectData GetObjectByPath(string repositoryId, string path, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds, bool? includeAcl,
            IExtensionsData extension);

        IContentStream GetContentStream(string repositoryId, string objectId, string streamId, long? offset, long? length,
            IExtensionsData extension);

        void UpdateProperties(string repositoryId, ref string objectId, ref string changeToken, IProperties properties,
            IExtensionsData extension);

        void MoveObject(string repositoryId, ref string objectId, string targetFolderId, string sourceFolderId,
            IExtensionsData extension);

        void DeleteObject(string repositoryId, string objectId, bool? allVersions, IExtensionsData extension);

        IFailedToDeleteData DeleteTree(string repositoryId, string folderId, bool? allVersions, UnfileObject? unfileObjects,
            bool? continueOnFailure, ExtensionsData extension);

        void SetContentStream(string repositoryId, ref string objectId, bool? overwriteFlag, ref string changeToken,
            IContentStream contentStream, IExtensionsData extension);

        void DeleteContentStream(string repositoryId, ref string objectId, ref string changeToken, IExtensionsData extension);
    }

    public interface IVersioningService
    {
        void CheckOut(string repositoryId, ref string objectId, IExtensionsData extension, out bool? contentCopied);

        void CancelCheckOut(string repositoryId, string objectId, IExtensionsData extension);

        void CheckIn(string repositoryId, ref string objectId, bool? major, IProperties properties,
            IContentStream contentStream, string checkinComment, IList<string> policies, IAcl addAces, IAcl removeAces,
            IExtensionsData extension);

        IObjectData GetObjectOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships,
            string renditionFilter, bool? includePolicyIds, bool? includeAcl, IExtensionsData extension);

        IProperties GetPropertiesOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, IExtensionsData extension);

        IList<IObjectData> GetAllVersions(string repositoryId, string objectId, string versionSeriesId, string filter,
            bool? includeAllowableActions, IExtensionsData extension);
    }

    public interface IRelationshipService
    {
        IObjectList GetObjectRelationships(string repositoryId, string objectId, bool? includeSubRelationshipTypes,
            RelationshipDirection? relationshipDirection, string typeId, string filter, bool? includeAllowableActions,
            long? maxItems, long? skipCount, IExtensionsData extension);
    }

    public interface IDiscoveryService
    {
        IObjectList Query(string repositoryId, string statement, bool? searchAllVersions,
           bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
           long? maxItems, long? skipCount, IExtensionsData extension);

        IObjectList GetContentChanges(string repositoryId, ref string changeLogToken, bool? includeProperties,
           string filter, bool? includePolicyIds, bool? includeAcl, long? maxItems, IExtensionsData extension);
    }

    public interface IMultiFilingService
    {
        void AddObjectToFolder(string repositoryId, string objectId, string folderId, bool? allVersions, IExtensionsData extension);

        void RemoveObjectFromFolder(string repositoryId, string objectId, string folderId, IExtensionsData extension);
    }

    public interface IAclService
    {
        IAcl GetAcl(string repositoryId, string objectId, bool? onlyBasicPermissions, IExtensionsData extension);

        IAcl ApplyAcl(string repositoryId, string objectId, IAcl addAces, IAcl removeAces, AclPropagation? aclPropagation,
            IExtensionsData extension);
    }

    public interface IPolicyService
    {
        void ApplyPolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension);

        void RemovePolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension);

        IList<IObjectData> GetAppliedPolicies(string repositoryId, string objectId, string filter, IExtensionsData extension);
    }
}
