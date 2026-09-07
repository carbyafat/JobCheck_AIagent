using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將單一 Persistence DTO 轉成 UTF-8 JSON，或從 JSON 還原 DTO。
    /// 不依賴 UnityEngine，因此此層可在 EditMode 測試與一般 .NET 環境共用。
    /// </summary>
    public static class PersistenceJsonSerializer
    {
        /// <summary>
        /// 將 DTO 序列化成不含 BOM 的 UTF-8 JSON 字串。
        /// </summary>
        public static string Serialize<T>(T value)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// 嘗試解析完整 JSON；格式錯誤時不丟出到 UI，而是回傳可記錄的錯誤文字。
        /// </summary>
        public static bool TryDeserialize<T>(
            string json,
            out T value,
            out string errorMessage)
            where T : class
        {
            value = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "JSON 內容為空白。";
                return false;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    value = serializer.ReadObject(stream) as T;
                }

                if (value == null)
                {
                    errorMessage = "JSON 未能建立預期的 DTO。";
                    return false;
                }

                return true;
            }
            catch (Exception exception) when (
                exception is System.Runtime.Serialization.SerializationException
                || exception is FormatException
                || exception is ArgumentException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }
    }
}
