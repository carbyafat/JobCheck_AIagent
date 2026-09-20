using System;
using System.Security.Cryptography;
using System.Text;

namespace JobCheck.Persistence
{
    /// <summary>個人履歷母資料的獨立搬運格式；不與職缺資料搬運檔混用。</summary>
    [Serializable]
    public sealed class CareerProfilePortablePackageDto
    {
        public const string CurrentFormatVersion = "1";
        public const string FileExtension = ".jobcheck-profile.json";

        public string format_version;
        public string exported_at;
        public string content_sha256;
        public CareerProfileDto profile;

        /// <summary>對不含自身 checksum 的標準序列化內容計算 SHA-256。</summary>
        public string CalculateContentSha256()
        {
            string saved = content_sha256;
            try
            {
                content_sha256 = null;
                byte[] bytes = Encoding.UTF8.GetBytes(
                    PersistenceJsonSerializer.Serialize(this));
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(bytes);
                    var result = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash)
                    {
                        result.Append(value.ToString("x2"));
                    }

                    return result.ToString();
                }
            }
            finally
            {
                content_sha256 = saved;
            }
        }
    }
}
