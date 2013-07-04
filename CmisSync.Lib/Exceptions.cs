//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Runtime.Serialization;
using System.Security.Permissions;


namespace CmisSync.Lib
{

    [Serializable]
    public class QuotaExceededException : Exception
    {

        public readonly int QuotaLimit = -1;


        public QuotaExceededException()
        {
        }


        public QuotaExceededException(string message, int quota_limit)
            : base(message)
        {
            QuotaLimit = quota_limit;
        }


        public QuotaExceededException(string message, Exception inner)
            : base(message, inner)
        {
        }


        protected QuotaExceededException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            info.AddValue("QuotaLimit", QuotaLimit);
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (null == info)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("QuotaLimit", QuotaLimit);
            base.GetObjectData(info, context);
        }

    }
}
