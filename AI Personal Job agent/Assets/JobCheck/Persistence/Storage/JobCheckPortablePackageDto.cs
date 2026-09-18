using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace JobCheck.Persistence
{
    /// <summary>單一檔案的 JobCheck 個人資料搬運格式；不包含機器上的絕對路徑。</summary>
    [Serializable]
    public sealed class JobCheckPortablePackageDto
    {
        public const string CurrentFormatVersion = "0.2";
        public const string FileExtension = ".jobcheck.json";

        public string format_version;
        public string exported_at;
        public string content_sha256;
        public int company_count;
        public int job_count;
        public int application_count;
        public List<CompanyDto> companies = new List<CompanyDto>();
        public List<JobPostingDto> jobs = new List<JobPostingDto>();
        public List<ApplicationDto> applications = new List<ApplicationDto>();

        /// <summary>對不含自身 checksum 的標準序列化內容計算 SHA-256。</summary>
        public string CalculateContentSha256()
        {
            string saved = content_sha256;
            try
            {
                content_sha256 = null;
                byte[] bytes = Encoding.UTF8.GetBytes(PersistenceJsonSerializer.Serialize(this));
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(bytes);
                    var result = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash) result.Append(value.ToString("x2"));
                    return result.ToString();
                }
            }
            finally
            {
                content_sha256 = saved;
            }
        }
    }

    public sealed class JobCheckPortableExportSummary
    {
        public JobCheckPortableExportSummary(string path, int companies, int jobs, int applications)
        {
            Path = path;
            CompanyCount = companies;
            JobCount = jobs;
            ApplicationCount = applications;
        }

        public string Path { get; }
        public int CompanyCount { get; }
        public int JobCount { get; }
        public int ApplicationCount { get; }
    }
}
