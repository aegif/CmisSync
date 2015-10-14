using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

namespace CmisSync.Lib.Auth
{
    /// <summary>
    /// Typical user credentials used for generic logins
    /// </summary>
    [Serializable]
    public class UserCredentials
    {
        /// <summary>
        /// User name
        /// </summary>
        [XmlElement("user")]
        public string UserName { get; set; }
        /// <summary>
        /// Password
        /// </summary>
        [XmlElement("password")]
        public Password Password { get; set; }
    }

    ///// <summary>
    ///// Server Login for a specific Uri
    ///// </summary>
    //[Serializable]
    //public class ServerCredentials : UserCredentials
    //{
    //    /// <summary>
    //    /// Server Address and Path
    //    /// </summary>
    //    public Uri Address { get; set; }

    //    /*public ServerCredentials(Uri Address, string UserName, Password Password)
    //    {
    //        this.Address = Address;
    //        this.UserName = UserName;
    //        this.Password = Password;
    //    }*/
    //}

    ///// <summary>
    ///// Credentials needed to create a Session for a specific CMIS repository
    ///// </summary>
    //[Serializable]
    //public class CmisRepoCredentials : ServerCredentials
    //{
    //    /// <summary>
    //    /// Repository ID
    //    /// </summary>
    //    public string RepoId { get; set; }
    //}

    /// <summary>
    /// Password class stores the given password obfuscated
    /// </summary>
    [Serializable]
    public class Password
    {
        private string password = null;
        /// <summary>
        /// Constructor initializing the instance with the given password
        /// </summary>
        /// <param name="password">as plain text</param>
        public Password(string password)
        {
            this.password = Crypto.Obfuscate(password);
        }

        /// <summary>
        /// Default constructor without setting the stored password
        /// </summary>
        public Password() { }

        /// <summary>
        /// Implizit contructor for passing a plain text string as password
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator Password(string value)
        {
            return new Password(value);
        }

        /// <summary>
        /// implicit.
        /// </summary>
        public static implicit operator string(Password o)
        {
            return o == null ? null : o.ToString();
        }

        /// <summary>
        /// Returns the password as plain text
        /// </summary>
        /// <returns>plain text password</returns>
        override
        public string ToString()
        {
            if (password == null)
                return null;
            return Crypto.Deobfuscate(password);
        }

        /// <summary>
        /// Gets and sets the internal saved and obfuscated password
        /// </summary>
        [XmlAttribute("obfuscated")]
        public string ObfuscatedPassword { get { return password; } set { password = value; } }
    }
}
