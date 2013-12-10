using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Auth
{
    public class CmisPassword
    {
        private string password = null;
        public CmisPassword(string password)
        {
            this.password = Crypto.Obfuscate(password);
        }

        public CmisPassword() { }

        public static implicit operator CmisPassword(string value)
        {
            return new CmisPassword(value);
        }
        override
        public string ToString()
        {
            if (password == null)
                return null;
            return Crypto.Deobfuscate(password);
        }

        public string ObfuscatedPassword { get { return password; } set { password = value; } }
    }
}
