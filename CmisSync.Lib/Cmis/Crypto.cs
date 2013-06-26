﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Obfuscation for sensitive data, making password harvesting a little less straightforward.
    /// Web browsers employ the same technique to store user passwords.
    /// </summary>
    public static class Crypto
    {
        /// <summary>
        /// Obfuscate a string.
        /// </summary>
        /// <param name="value">The string to obfuscate</param>
        /// <returns>The obfuscated string</returns>
        public static string Obfuscate(string value)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return WindowsObfuscate(value);
            }
            else
            {
                return UnixObfuscate(value);
            }
        }


        /// <summary>
        /// Deobfuscate a string.
        /// </summary>
        /// <param name="value">The string to deobfuscate</param>
        /// <returns>The clear string</returns>
        public static string Deobfuscate(string value)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return WindowsDeobfuscate(value);
            }
            else
            {
                return UnixDeobfuscate(value);
            }
        }


        /// <summary>
        /// Obfuscate a string on Windows.
        /// We use the recommended API for this: DPAPI (Windows Data Protection API)
        /// http://msdn.microsoft.com/en-us/library/ms995355.aspx
        /// Warning: Even though it uses the Windows user's password, it is not uncrackable.
        /// </summary>
        /// <param name="value">The string to obfuscate</param>
        /// <returns>The obfuscated string</returns>
        private static string WindowsObfuscate(string value)
        {
            #if __MonoCS__
                // This macro prevents compilation errors on Unix where ProtectedData does not exist.
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


        private static string WindowsDeobfuscate(string value)
        {
            #if __MonoCS__
                // This macro prevents compilation errors on Unix where ProtectedData does not exist.
                throw new ApplicationException("Should never be reached");
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


        /// <summary>
        /// Obfuscate a string on Unix.
        /// AES is used.
        /// </summary>
        /// <param name="value">The string to obfuscate</param>
        /// <returns>The obfuscated string</returns>
        private static string UnixObfuscate(string value)
        {
#if __MonoCS__
            try
            {
                AesManaged myAes = new AesManaged();
                myAes.Mode = CipherMode.CBC;
                myAes.IV = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                myAes.Key = GetCryptoKeyUnix();  // Byte array representing the key. Key MUST be 256 bits
                myAes.Padding = PaddingMode.PKCS7;

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
#else
            throw new ApplicationException("Should never be reached");
#endif
        }


        private static string UnixDeobfuscate(string value)
        {
#if __MonoCS__
            try
            {
                AesManaged myAes = new AesManaged();
                myAes.Mode = CipherMode.CBC;
                myAes.IV = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                myAes.Key = GetCryptoKeyUnix();  // Byte array representing the key. Key MUST be 256 bits
                myAes.Padding = PaddingMode.PKCS7;

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
#else
            throw new ApplicationException("Should never be reached");
#endif
        }


        /// <summary>
        /// Salt for the obfuscation.
        /// </summary>
        public static byte[] GetCryptoKey()
        {
            return System.Text.Encoding.UTF8.GetBytes(
                "Thou art so farth away, I miss you my dear files❥, with CmisSync be forever by my side!");
        }

#if __MonoCS__
        /// <summary>
        /// Salt for the obfuscation. MUST be 256 bits.
        /// </summary>
        public static byte[] GetCryptoKeyUnix()
        {
            return System.Text.Encoding.UTF8.GetBytes("Thou art so farth away, I miss y");
        }
#endif

    }

}
