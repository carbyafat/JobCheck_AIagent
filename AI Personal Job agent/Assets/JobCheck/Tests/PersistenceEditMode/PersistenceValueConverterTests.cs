using System;
using NUnit.Framework;
using JobCheck.Domain;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證 V0.2 時間與 enum JSON 字串轉換不會遺失時區或接受未知值。
    /// </summary>
    public class PersistenceValueConverterTests
    {
        /// <summary>
        /// 確認格式化時間後仍保留原始 +08:00 offset。
        /// </summary>
        [Test]
        public void DateTimeFormat_OffsetValue_PreservesOffset()
        {
            var value = new DateTimeOffset(
                2026,
                9,
                6,
                14,
                30,
                0,
                TimeSpan.FromHours(8));

            string formatted = PersistenceDateTimeConverter.Format(value);

            StringAssert.EndsWith("+08:00", formatted);
        }

        /// <summary>
        /// 確認 nullable 時間沒有值時輸出 null，而不是空字串或虛構日期。
        /// </summary>
        [Test]
        public void DateTimeFormat_Null_ReturnsNull()
        {
            Assert.IsNull(PersistenceDateTimeConverter.Format(null));
        }

        /// <summary>
        /// 確認含明確 offset 的 ISO 8601 字串能正確解析且保留 offset。
        /// </summary>
        [Test]
        public void DateTimeParse_ExplicitOffset_ReturnsOriginalValue()
        {
            bool success = PersistenceDateTimeConverter.TryParseRequired(
                "2026-09-06T14:30:00+08:00",
                out DateTimeOffset parsed);

            Assert.IsTrue(success);
            Assert.AreEqual(TimeSpan.FromHours(8), parsed.Offset);
            Assert.AreEqual(14, parsed.Hour);
        }

        /// <summary>
        /// 確認 UTC Z 格式也是有效且不會被轉成本機時區。
        /// </summary>
        [Test]
        public void DateTimeParse_UtcZ_ReturnsZeroOffset()
        {
            bool success = PersistenceDateTimeConverter.TryParseRequired(
                "2026-09-06T06:30:00Z",
                out DateTimeOffset parsed);

            Assert.IsTrue(success);
            Assert.AreEqual(TimeSpan.Zero, parsed.Offset);
        }

        /// <summary>
        /// 確認缺少 offset 的時間不會被靜默套用目前電腦時區。
        /// </summary>
        [Test]
        public void DateTimeParse_MissingOffset_ReturnsFalse()
        {
            bool success = PersistenceDateTimeConverter.TryParseRequired(
                "2026-09-06T14:30:00",
                out _);

            Assert.IsFalse(success);
        }

        /// <summary>
        /// 確認日期內容本身無效時解析失敗。
        /// </summary>
        [Test]
        public void DateTimeParse_InvalidDate_ReturnsFalse()
        {
            bool success = PersistenceDateTimeConverter.TryParseRequired(
                "2026-02-31T14:30:00+08:00",
                out _);

            Assert.IsFalse(success);
        }

        /// <summary>
        /// 確認 optional 時間的 null 表示合法缺漏。
        /// </summary>
        [Test]
        public void DateTimeParseOptional_Null_ReturnsSuccessfulNull()
        {
            bool success = PersistenceDateTimeConverter.TryParseOptional(null, out DateTimeOffset? parsed);

            Assert.IsTrue(success);
            Assert.IsNull(parsed);
        }

        /// <summary>
        /// 確認 optional 時間若有欄位但只填空白，仍會視為格式錯誤而不是缺漏。
        /// </summary>
        [Test]
        public void DateTimeParseOptional_Blank_ReturnsFalse()
        {
            bool success = PersistenceDateTimeConverter.TryParseOptional(" ", out _);

            Assert.IsFalse(success);
        }

        /// <summary>
        /// 確認 PascalCase enum 會輸出 V0.2 小寫 snake_case。
        /// </summary>
        [Test]
        public void EnumFormat_PascalCase_ReturnsLowerSnakeCase()
        {
            Assert.AreEqual(
                "waiting_response",
                PersistenceEnumConverter.Format(ApplicationStage.WaitingResponse));
            Assert.AreEqual(
                "no_response_marked",
                PersistenceEnumConverter.Format(ApplicationEventType.NoResponseMarked));
        }

        /// <summary>
        /// 確認合法 snake_case enum 字串可解析成 Domain enum。
        /// </summary>
        [Test]
        public void EnumParse_KnownValue_ReturnsDomainEnum()
        {
            bool success = PersistenceEnumConverter.TryParse(
                "closed_by_candidate",
                out ApplicationStage parsed);

            Assert.IsTrue(success);
            Assert.AreEqual(ApplicationStage.ClosedByCandidate, parsed);
        }

        /// <summary>
        /// 確認 enum JSON 值大小寫不符時不會被寬鬆接受。
        /// </summary>
        [Test]
        public void EnumParse_WrongCase_ReturnsFalse()
        {
            bool success = PersistenceEnumConverter.TryParse(
                "Waiting_Response",
                out ApplicationStage _);

            Assert.IsFalse(success);
        }

        /// <summary>
        /// 確認未知 enum 字串不會被轉成 enum 的數值 0。
        /// </summary>
        [Test]
        public void EnumParse_UnknownValue_ReturnsFalse()
        {
            bool success = PersistenceEnumConverter.TryParse(
                "banana",
                out ApplicationStage _);

            Assert.IsFalse(success);
        }

        /// <summary>
        /// 確認未定義的 enum 數值無法被寫入 JSON。
        /// </summary>
        [Test]
        public void EnumFormat_UndefinedValue_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                PersistenceEnumConverter.Format((ApplicationStage)999));
        }

        /// <summary>
        /// 確認目前所有 Domain enum 的每個已定義值都能格式化後再解析回原值。
        /// </summary>
        [Test]
        public void EnumConverter_AllDomainEnums_RoundTrip()
        {
            AssertEnumRoundTrip<ApplicationEventType>();
            AssertEnumRoundTrip<ApplicationStage>();
            AssertEnumRoundTrip<EventActor>();
            AssertEnumRoundTrip<SourceType>();
            AssertEnumRoundTrip<CandidateCloseReason>();
            AssertEnumRoundTrip<ArchiveReason>();
        }

        /// <summary>
        /// 對指定 enum 的全部已定義值執行字串 round-trip。
        /// </summary>
        private static void AssertEnumRoundTrip<TEnum>()
            where TEnum : struct
        {
            foreach (object valueObject in Enum.GetValues(typeof(TEnum)))
            {
                var original = (TEnum)valueObject;
                string formatted = PersistenceEnumConverter.Format(original);
                bool success = PersistenceEnumConverter.TryParse(formatted, out TEnum parsed);

                Assert.IsTrue(success, "Could not parse {0}.{1}.", typeof(TEnum).Name, original);
                Assert.AreEqual(original, parsed);
            }
        }
    }
}
