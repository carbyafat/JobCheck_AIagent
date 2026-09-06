using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 在 Company Domain 模型與 CompanyDto JSON 形狀之間搬運資料。
    /// </summary>
    public static class CompanyDtoMapper
    {
        /// <summary>
        /// 將 DTO 轉成 Domain Company；本方法不執行 Company 業務規則驗證。
        /// </summary>
        public static PersistenceConversionResult<Company> ToDomain(CompanyDto dto)
        {
            var context = new PersistenceMappingContext();
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "company");
                return context.CreateResult<Company>(null);
            }

            var company = new Company
            {
                Id = dto.id,
                SchemaVersion = dto.schema_version,
                Name = dto.name,
                Industry = dto.industry,
                Notes = dto.notes,
                RiskFlags = PersistenceListMapper.Copy(dto.risk_flags),
                CreatedAt = context.ParseOptionalDateTime(dto.created_at, "created_at"),
                UpdatedAt = context.ParseOptionalDateTime(dto.updated_at, "updated_at")
            };

            return context.CreateResult(company);
        }

        /// <summary>
        /// 將 Domain Company 轉成 DTO；集合會被複製，不與 Domain 共用 List。
        /// </summary>
        public static PersistenceConversionResult<CompanyDto> ToDto(Company company)
        {
            var context = new PersistenceMappingContext();
            if (company == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "company");
                return context.CreateResult<CompanyDto>(null);
            }

            var dto = new CompanyDto
            {
                id = company.Id,
                schema_version = company.SchemaVersion,
                name = company.Name,
                industry = company.Industry,
                notes = company.Notes,
                risk_flags = PersistenceListMapper.Copy(company.RiskFlags),
                created_at = PersistenceDateTimeConverter.Format(company.CreatedAt),
                updated_at = PersistenceDateTimeConverter.Format(company.UpdatedAt)
            };

            return context.CreateResult(dto);
        }
    }
}
