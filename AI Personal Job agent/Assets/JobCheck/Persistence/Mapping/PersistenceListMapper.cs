using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// Mapper 共用的集合複製工具，避免 DTO 與 Domain 共用同一個可變 List。
    /// </summary>
    internal static class PersistenceListMapper
    {
        public static List<T> Copy<T>(IEnumerable<T> source)
        {
            return source == null ? new List<T>() : new List<T>(source);
        }
    }
}
