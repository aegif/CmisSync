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

namespace DotCMIS.Exceptions
{
    /// <summary>
    /// Base exception for all CMIS exceptions.
    /// </summary>
    [Serializable]
    public class CmisBaseException : ApplicationException
    {
        public CmisBaseException() { Code = null; }
        public CmisBaseException(string message) : base(message) { Code = null; }
        public CmisBaseException(string message, Exception inner) : base(message, inner) { Code = null; }
        protected CmisBaseException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }

        public CmisBaseException(string message, long? code)
            : this(message)
        {
            Code = code;
        }

        public CmisBaseException(string message, string errorContent)
            : this(message)
        {
            ErrorContent = errorContent;
        }

        public CmisBaseException(string message, string errorContent, Exception inner)
            : this(message, inner)
        {
            ErrorContent = errorContent;
        }

        public long? Code { get; protected set; }
        public string ErrorContent { get; protected set; }
    }

    [Serializable]
    public class CmisConnectionException : CmisBaseException
    {
        public CmisConnectionException() : base() { }
        public CmisConnectionException(string message) : base(message) { }
        public CmisConnectionException(string message, Exception inner) : base(message, inner) { }
        protected CmisConnectionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisConnectionException(string message, long? code)
            : base(message) { }
        public CmisConnectionException(string message, string errorContent)
            : base(message) { }
        public CmisConnectionException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisConstraintException : CmisBaseException
    {
        public CmisConstraintException() : base() { }
        public CmisConstraintException(string message) : base(message) { }
        public CmisConstraintException(string message, Exception inner) : base(message, inner) { }
        protected CmisConstraintException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisConstraintException(string message, long? code)
            : base(message) { }
        public CmisConstraintException(string message, string errorContent)
            : base(message) { }
        public CmisConstraintException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisContentAlreadyExistsException : CmisBaseException
    {
        public CmisContentAlreadyExistsException() : base() { }
        public CmisContentAlreadyExistsException(string message) : base(message) { }
        public CmisContentAlreadyExistsException(string message, Exception inner) : base(message, inner) { }
        protected CmisContentAlreadyExistsException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisContentAlreadyExistsException(string message, long? code)
            : base(message) { }
        public CmisContentAlreadyExistsException(string message, string errorContent)
            : base(message) { }
        public CmisContentAlreadyExistsException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisFilterNotValidException : CmisBaseException
    {
        public CmisFilterNotValidException() : base() { }
        public CmisFilterNotValidException(string message) : base(message) { }
        public CmisFilterNotValidException(string message, Exception inner) : base(message, inner) { }
        protected CmisFilterNotValidException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisFilterNotValidException(string message, long? code)
            : base(message) { }
        public CmisFilterNotValidException(string message, string errorContent)
            : base(message) { }
        public CmisFilterNotValidException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisInvalidArgumentException : CmisBaseException
    {
        public CmisInvalidArgumentException() : base() { }
        public CmisInvalidArgumentException(string message) : base(message) { }
        public CmisInvalidArgumentException(string message, Exception inner) : base(message, inner) { }
        protected CmisInvalidArgumentException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisInvalidArgumentException(string message, long? code)
            : base(message) { }
        public CmisInvalidArgumentException(string message, string errorContent)
            : base(message) { }
        public CmisInvalidArgumentException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisNameConstraintViolationException : CmisBaseException
    {
        public CmisNameConstraintViolationException() : base() { }
        public CmisNameConstraintViolationException(string message) : base(message) { }
        public CmisNameConstraintViolationException(string message, Exception inner) : base(message, inner) { }
        protected CmisNameConstraintViolationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisNameConstraintViolationException(string message, long? code)
            : base(message) { }
        public CmisNameConstraintViolationException(string message, string errorContent)
            : base(message) { }
        public CmisNameConstraintViolationException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisNotSupportedException : CmisBaseException
    {
        public CmisNotSupportedException() : base() { }
        public CmisNotSupportedException(string message) : base(message) { }
        public CmisNotSupportedException(string message, Exception inner) : base(message, inner) { }
        protected CmisNotSupportedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisNotSupportedException(string message, long? code)
            : base(message) { }
        public CmisNotSupportedException(string message, string errorContent)
            : base(message) { }
        public CmisNotSupportedException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisObjectNotFoundException : CmisBaseException
    {
        public CmisObjectNotFoundException() : base() { }
        public CmisObjectNotFoundException(string message) : base(message) { }
        public CmisObjectNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected CmisObjectNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisObjectNotFoundException(string message, long? code)
            : base(message) { }
        public CmisObjectNotFoundException(string message, string errorContent)
            : base(message) { }
        public CmisObjectNotFoundException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisPermissionDeniedException : CmisBaseException
    {
        public CmisPermissionDeniedException() : base() { }
        public CmisPermissionDeniedException(string message) : base(message) { }
        public CmisPermissionDeniedException(string message, Exception inner) : base(message, inner) { }
        protected CmisPermissionDeniedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisPermissionDeniedException(string message, long? code)
            : base(message) { }
        public CmisPermissionDeniedException(string message, string errorContent)
            : base(message) { }
        public CmisPermissionDeniedException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisRuntimeException : CmisBaseException
    {
        public CmisRuntimeException() : base() { }
        public CmisRuntimeException(string message) : base(message) { }
        public CmisRuntimeException(string message, Exception inner) : base(message, inner) { }
        protected CmisRuntimeException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisRuntimeException(string message, long? code)
            : base(message) { }
        public CmisRuntimeException(string message, string errorContent)
            : base(message) { }
        public CmisRuntimeException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisStorageException : CmisBaseException
    {
        public CmisStorageException() : base() { }
        public CmisStorageException(string message) : base(message) { }
        public CmisStorageException(string message, Exception inner) : base(message, inner) { }
        protected CmisStorageException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisStorageException(string message, long? code)
            : base(message) { }
        public CmisStorageException(string message, string errorContent)
            : base(message) { }
        public CmisStorageException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisStreamNotSupportedException : CmisBaseException
    {
        public CmisStreamNotSupportedException() : base() { }
        public CmisStreamNotSupportedException(string message) : base(message) { }
        public CmisStreamNotSupportedException(string message, Exception inner) : base(message, inner) { }
        protected CmisStreamNotSupportedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisStreamNotSupportedException(string message, long? code)
            : base(message) { }
        public CmisStreamNotSupportedException(string message, string errorContent)
            : base(message) { }
        public CmisStreamNotSupportedException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisUpdateConflictException : CmisBaseException
    {
        public CmisUpdateConflictException() : base() { }
        public CmisUpdateConflictException(string message) : base(message) { }
        public CmisUpdateConflictException(string message, Exception inner) : base(message, inner) { }
        protected CmisUpdateConflictException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisUpdateConflictException(string message, long? code)
            : base(message) { }
        public CmisUpdateConflictException(string message, string errorContent)
            : base(message) { }
        public CmisUpdateConflictException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }

    [Serializable]
    public class CmisVersioningException : CmisBaseException
    {
        public CmisVersioningException() : base() { }
        public CmisVersioningException(string message) : base(message) { }
        public CmisVersioningException(string message, Exception inner) : base(message, inner) { }
        protected CmisVersioningException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        public CmisVersioningException(string message, long? code)
            : base(message) { }
        public CmisVersioningException(string message, string errorContent)
            : base(message) { }
        public CmisVersioningException(string message, string errorContent, Exception inner)
            : base(message, errorContent, inner) { }
    }
}
