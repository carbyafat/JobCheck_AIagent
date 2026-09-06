using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JobCheck.Persistence
{
    /// <summary>
    /// DTO／Domain 轉換結果。只要有任何 Issue，Value 必定為 null，避免誤用半套資料。
    /// </summary>
    public sealed class PersistenceConversionResult<T>
        where T : class
    {
        /// <summary>
        /// 建立成功或失敗結果；issues 不是空集合時會捨棄 value。
        /// </summary>
        public PersistenceConversionResult(
            T value,
            IEnumerable<PersistenceConversionIssue> issues)
        {
            var issueSnapshot = issues == null
                ? Array.Empty<PersistenceConversionIssue>()
                : new List<PersistenceConversionIssue>(issues).ToArray();

            Issues = new ReadOnlyCollection<PersistenceConversionIssue>(issueSnapshot);
            Value = issueSnapshot.Length == 0 ? value : null;
        }

        /// <summary>
        /// 轉換成功後的完整物件；失敗時必定為 null。
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 所有可定位的轉換問題；集合一定存在且不可由呼叫端修改。
        /// </summary>
        public IReadOnlyList<PersistenceConversionIssue> Issues { get; }

        /// <summary>
        /// 沒有問題且 Value 存在時為 true。
        /// </summary>
        public bool IsSuccess => Value != null && Issues.Count == 0;
    }
}
