using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ServiceActor
{
    internal static class Utils
    {
        public static string MD5Hash(string input)
        {
            using (var md5provider = new MD5CryptoServiceProvider())
            {
                byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(input));

                var hash = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash.Append(bytes[i].ToString("x2"));
                }
                return hash.ToString();
            }
        }
    }
}
