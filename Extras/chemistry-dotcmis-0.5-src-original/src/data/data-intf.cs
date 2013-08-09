using System;
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
using System.IO;
using DotCMIS.Data.Extensions;
using DotCMIS.Enums;

namespace DotCMIS.Data
{
    public interface IRepositoryInfo : IExtensionsData
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string VendorName { get; }
        string ProductName { get; }
        string ProductVersion { get; }
        string RootFolderId { get; }
        IRepositoryCapabilities Capabilities { get; }
        IAclCapabilities AclCapabilities { get; }
        string LatestChangeLogToken { get; }
        string CmisVersionSupported { get; }
        string ThinClientUri { get; }
        bool? ChangesIncomplete { get; }
        IList<BaseTypeId?> ChangesOnType { get; }
        string PrincipalIdAnonymous { get; }
        string PrincipalIdAnyone { get; }
    }

    public interface IRepositoryCapabilities : IExtensionsData
    {
        CapabilityContentStreamUpdates? ContentStreamUpdatesCapability { get; }
        CapabilityChanges? ChangesCapability { get; }
        CapabilityRenditions? RenditionsCapability { get; }
        bool? IsGetDescendantsSupported { get; }
        bool? IsGetFolderTreeSupported { get; }
        bool? IsMultifilingSupported { get; }
        bool? IsUnfilingSupported { get; }
        bool? IsVersionSpecificFilingSupported { get; }
        bool? IsPwcSearchableSupported { get; }
        bool? IsPwcUpdatableSupported { get; }
        bool? IsAllVersionsSearchableSupported { get; }
        CapabilityQuery? QueryCapability { get; }
        CapabilityJoin? JoinCapability { get; }
        CapabilityAcl? AclCapability { get; }
    }

    public interface IAclCapabilities : IExtensionsData
    {
        SupportedPermissions? SupportedPermissions { get; }
        AclPropagation? AclPropagation { get; }
        IList<IPermissionDefinition> Permissions { get; }
        IDictionary<string, IPermissionMapping> PermissionMapping { get; }
    }

    public interface IPermissionDefinition : IExtensionsData
    {
        string Id { get; }
        string Description { get; }
    }

    public interface IPermissionMapping : IExtensionsData
    {
        string Key { get; }
        IList<string> Permissions { get; }
    }

    public interface ITypeDefinition : IExtensionsData
    {
        string Id { get; }
        string LocalName { get; }
        string LocalNamespace { get; }
        string DisplayName { get; }
        string QueryName { get; }
        string Description { get; }
        BaseTypeId BaseTypeId { get; }
        string ParentTypeId { get; }
        bool? IsCreatable { get; }
        bool? IsFileable { get; }
        bool? IsQueryable { get; }
        bool? IsFulltextIndexed { get; }
        bool? IsIncludedInSupertypeQuery { get; }
        bool? IsControllablePolicy { get; }
        bool? IsControllableAcl { get; }
        IPropertyDefinition this[string propertyId] { get; }
        IList<IPropertyDefinition> PropertyDefinitions { get; }
    }

    public interface IDocumentTypeDefinition : ITypeDefinition
    {
        bool? IsVersionable { get; }
        ContentStreamAllowed? ContentStreamAllowed { get; }
    }

    public interface IFolderTypeDefinition : ITypeDefinition
    {
    }

    public interface IPolicyTypeDefinition : ITypeDefinition
    {
    }

    public interface IRelationshipTypeDefinition : ITypeDefinition
    {
        IList<string> AllowedSourceTypeIds { get; }
        IList<string> AllowedTargetTypeIds { get; }
    }

    public interface ITypeDefinitionList : IExtensionsData
    {
        IList<ITypeDefinition> List { get; }
        bool? HasMoreItems { get; }
        long? NumItems { get; }
    }

    public interface ITypeDefinitionContainer : IExtensionsData
    {
        ITypeDefinition TypeDefinition { get; }
        IList<ITypeDefinitionContainer> Children { get; }
    }

    public interface IPropertyDefinition : IExtensionsData
    {
        string Id { get; }
        string LocalName { get; }
        string LocalNamespace { get; }
        string DisplayName { get; }
        string QueryName { get; }
        string Description { get; }
        PropertyType PropertyType { get; }
        Cardinality Cardinality { get; }
        Updatability Updatability { get; }
        bool? IsInherited { get; }
        bool? IsRequired { get; }
        bool? IsQueryable { get; }
        bool? IsOrderable { get; }
        bool? IsOpenChoice { get; }
    }

    public interface IChoice<T>
    {
        string DisplayName { get; }
        IList<T> Value { get; }
        IList<IChoice<T>> Choices { get; }
    }

    public interface IPropertyBooleanDefinition : IPropertyDefinition
    {
        IList<bool> DefaultValue { get; }
        IList<IChoice<bool>> Choices { get; }
    }

    public interface IPropertyDateTimeDefinition : IPropertyDefinition
    {
        IList<DateTime> DefaultValue { get; }
        IList<IChoice<DateTime>> Choices { get; }
        DateTimeResolution? DateTimeResolution { get; }
    }

    public interface IPropertyDecimalDefinition : IPropertyDefinition
    {
        IList<decimal> DefaultValue { get; }
        IList<IChoice<decimal>> Choices { get; }
        decimal? MinValue { get; }
        decimal? MaxValue { get; }
        DecimalPrecision? Precision { get; }
    }

    public interface IPropertyHtmlDefinition : IPropertyDefinition
    {
        IList<string> DefaultValue { get; }
        IList<IChoice<string>> Choices { get; }
    }

    public interface IPropertyIdDefinition : IPropertyDefinition
    {
        IList<string> DefaultValue { get; }
        IList<IChoice<string>> Choices { get; }
    }

    public interface IPropertyIntegerDefinition : IPropertyDefinition
    {
        IList<long> DefaultValue { get; }
        IList<IChoice<long>> Choices { get; }
        long? MinValue { get; }
        long? MaxValue { get; }
    }

    public interface IPropertyStringDefinition : IPropertyDefinition
    {
        IList<string> DefaultValue { get; }
        IList<IChoice<string>> Choices { get; }
        long? MaxLength { get; }
    }

    public interface IPropertyUriDefinition : IPropertyDefinition
    {
        IList<string> DefaultValue { get; }
        IList<IChoice<string>> Choices { get; }
    }

    public interface IObjectData : IExtensionsData
    {
        string Id { get; }
        BaseTypeId? BaseTypeId { get; }
        IProperties Properties { get; }
        IAllowableActions AllowableActions { get; }
        IList<IObjectData> Relationships { get; }
        IChangeEventInfo ChangeEventInfo { get; }
        IAcl Acl { get; }
        bool? IsExactAcl { get; }
        IPolicyIdList PolicyIds { get; }
        IList<IRenditionData> Renditions { get; }
    }

    public interface IObjectList : IExtensionsData
    {
        IList<IObjectData> Objects { get; }
        bool? HasMoreItems { get; }
        long? NumItems { get; }
    }

    public interface IObjectInFolderData : IExtensionsData
    {
        IObjectData Object { get; }
        string PathSegment { get; }
    }

    public interface IObjectInFolderList : IExtensionsData
    {
        IList<IObjectInFolderData> Objects { get; }
        bool? HasMoreItems { get; }
        long? NumItems { get; }
    }

    public interface IObjectInFolderContainer : IExtensionsData
    {
        IObjectInFolderData Object { get; }
        IList<IObjectInFolderContainer> Children { get; }
    }

    public interface IObjectParentData : IExtensionsData
    {
        IObjectData Object { get; }
        string RelativePathSegment { get; }
    }

    public interface IProperties : IExtensionsData
    {
        IPropertyData this[string propertyId] { get; }
        IList<IPropertyData> PropertyList { get; }
    }

    public interface IPropertyData : IExtensionsData
    {
        string Id { get; }
        string LocalName { get; }
        string DisplayName { get; }
        string QueryName { get; }
        PropertyType PropertyType { get; }
        IList<object> Values { get; }
        object FirstValue { get; }
    }

    public interface IPrincipal : IExtensionsData
    {
        string Id { get; }
    }

    public interface IAce : IExtensionsData
    {
        IPrincipal Principal { get; }
        string PrincipalId { get; }
        IList<string> Permissions { get; }
        bool IsDirect { get; }
    }

    public interface IAcl : IExtensionsData
    {
        IList<IAce> Aces { get; }
        bool? IsExact { get; }
    }

    public interface IContentStream : IExtensionsData
    {
        long? Length { get; }
        string MimeType { get; }
        string FileName { get; }
        Stream Stream { get; }
    }

    public interface IAllowableActions : IExtensionsData
    {
        HashSet<string> Actions { get; }
    }

    public interface IRenditionData : IExtensionsData
    {
        string StreamId { get; }
        string MimeType { get; }
        long? Length { get; }
        string Kind { get; }
        string Title { get; }
        long? Height { get; }
        long? Width { get; }
        string RenditionDocumentId { get; }
    }

    public interface IChangeEventInfo : IExtensionsData
    {
        ChangeType? ChangeType { get; }
        DateTime? ChangeTime { get; }
    }

    public interface IPolicyIdList : IExtensionsData
    {
        IList<string> PolicyIds { get; }
    }

    public interface IFailedToDeleteData : IExtensionsData
    {
        IList<string> Ids { get; }
    }
}
