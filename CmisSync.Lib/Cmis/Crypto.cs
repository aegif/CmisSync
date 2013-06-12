using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace CmisSync.Lib.Cmis
{
    public static class Crypto
    {
        // Simple obfuscation using a hardcoded key.

        public static byte[] GetCryptoKey()
        {
            return System.Text.Encoding.UTF8.GetBytes(
                "Thou art so farth away, I miss you my dear files❥, with CmisSync be forever by my side!");
        }

        // Obfuscate a string.
        public static string Protect(string value)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return WindowsProtect(value);
            }
            else
            {
                return UnixProtect(value);
            }
        }

        // De-obfuscate a string.
        public static string Unprotect(string value)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return WindowsUnprotect(value);
            }
            else
            {
                return UnixUnprotect(value);
            }
        }

        // On Windows, we use the recommended DPAPI
        public static string WindowsProtect(string value)
        {
            #if __MonoCS__
                return "Should never be reached";
            #else
                try
                {
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(value);
                    // Encrypt the data using DataProtectionScope.CurrentUser. The result can be decrypted
                    //  only by the same current user.
                    byte[] crypt = ProtectedData.Protect(data, GetCryptoKey(), DataProtectionScope.CurrentUser);
                    return Convert.ToBase64String(crypt, Base64FormattingOptions.None);
                }
                catch (CryptographicException e)
                {
                    Console.WriteLine("Data was not encrypted. An error occurred.");
                    Console.WriteLine(e.ToString());
                    return null;
                }
            #endif
        }

        public static string WindowsUnprotect(string value)
        {
            #if __MonoCS__
                return "Should never be reached";
            #else
                try
                {
                    byte[] data = Convert.FromBase64String(value);
                    //Decrypt the data using DataProtectionScope.CurrentUser.
                    byte[] uncrypt = ProtectedData.Unprotect(data, GetCryptoKey(), DataProtectionScope.CurrentUser);
                    return System.Text.Encoding.UTF8.GetString(uncrypt);
                }
                catch (Exception e)
                {
                    if (e is CryptographicException || e is FormatException)
                    {
                        Console.WriteLine("Your password is not obfuscated yet.");
                        Console.WriteLine("Using unobfuscated value directly might be deprecated soon, so please delete your local directories and recreate them. Thank you for your understanding.");
                        return value;
                    }
                    else
                    {
                        throw;
                    }
                }
            #endif
        }

        
        public static string UnixProtect(string value)
        {
            try
            {
                AesManaged myAes = new AesManaged();
                myAes.Mode = CipherMode.ECB;
                myAes.IV = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // CRB mode uses an empty IV
                myAes.Key = GetCryptoKey();  // Byte array representing the key
                myAes.Padding = PaddingMode.None;

                ICryptoTransform encryptor = myAes.CreateEncryptor();

                byte[] data = System.Text.Encoding.UTF8.GetBytes(value);
                byte[] crypt = encryptor.TransformFinalBlock(data, 0, data.Length);
                return Convert.ToBase64String(crypt, Base64FormattingOptions.None);
            }

            catch (CryptographicException e)
            {
                Console.WriteLine("Data was not encrypted. An error occurred.");
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public static string UnixUnprotect(string value)
        {
            try
            {
                AesManaged myAes = new AesManaged();
                myAes.Mode = CipherMode.ECB;
                myAes.IV = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // CRB mode uses an empty IV
                myAes.Key = GetCryptoKey();  // Byte array representing the key
                myAes.Padding = PaddingMode.None;

                ICryptoTransform decryptor = myAes.CreateDecryptor();

                byte[] data = Convert.FromBase64String(value);

                byte[] uncrypt = decryptor.TransformFinalBlock(data, 0, data.Length);
                return System.Text.Encoding.UTF8.GetString(uncrypt);
            }
            catch (Exception e)
            {
                if (e is CryptographicException || e is FormatException)
                {
                    Console.WriteLine("Your password is not obfuscated yet.");
                    Console.WriteLine("Using unobfuscated value directly might be deprecated soon, so please delete your local directories and recreate them. Thank you for your understanding.");
                    return value;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
