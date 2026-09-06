using System;

namespace JobCheck.Domain
{
    /// <summary>
    /// 描述特定職缺的聘僱與工作條件，不應放入 Company 共用資料。
    /// </summary>
    [Serializable]
    public sealed class JobWorkConditions
    {
        /// <summary>
        /// 聘僱形式，例如全職、兼職或約聘。
        /// </summary>
        public string EmploymentType { get; set; }

        /// <summary>
        /// 上班時段或工時說明。
        /// </summary>
        public string WorkingHours { get; set; }

        /// <summary>
        /// 出差或外派需求。
        /// </summary>
        public string BusinessTrip { get; set; }

        /// <summary>
        /// 是否需要管理人員及其來源說明。
        /// </summary>
        public string ManagementResponsibility { get; set; }

        /// <summary>
        /// 休假制度說明。
        /// </summary>
        public string LeavePolicy { get; set; }

        /// <summary>
        /// 可到職日期或公司希望的開始時間。
        /// </summary>
        public string StartDate { get; set; }
    }
}
