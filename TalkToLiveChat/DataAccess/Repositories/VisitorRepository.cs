using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IVisitorRepository
    {
        Task<Visitor> FindOrCreateVisitorAsync(string countryCode, string mobileNumber);
        Task<List<VisitorSummary>> GetVisitorsListAsync(string? searchMobile);
    }

    public class VisitorRepository : IVisitorRepository
    {
        private readonly IDatabaseHelper _db;

        public VisitorRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<Visitor> FindOrCreateVisitorAsync(string countryCode, string mobileNumber)
        {
            var p1 = new SqlParameter("@CountryCode", SqlDbType.NVarChar, 10) { Value = countryCode };
            var p2 = new SqlParameter("@MobileNumber", SqlDbType.NVarChar, 20) { Value = mobileNumber };

            var visitor = await _db.ExecuteReaderSingleAsync(
                "dbo.sp_FindOrCreateVisitor",
                CommandType.StoredProcedure,
                reader => new Visitor
                {
                    VisitorId = reader.GetSafeInt32("VisitorId"),
                    CountryCode = reader.GetSafeString("CountryCode"),
                    MobileNumber = reader.GetSafeString("MobileNumber"),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate")
                },
                p1, p2
            );

            return visitor ?? new Visitor { CountryCode = countryCode, MobileNumber = mobileNumber };
        }

        public async Task<List<VisitorSummary>> GetVisitorsListAsync(string? searchMobile)
        {
            var p1 = new SqlParameter("@SearchMobile", SqlDbType.NVarChar, 50) { Value = (object?)searchMobile ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetVisitorsList",
                CommandType.StoredProcedure,
                reader => new VisitorSummary
                {
                    VisitorId = reader.GetSafeInt32("VisitorId"),
                    CountryCode = reader.GetSafeString("CountryCode"),
                    MobileNumber = reader.GetSafeString("MobileNumber"),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate"),
                    FirstContactDate = reader.GetSafeNullableDateTime("FirstContactDate"),
                    LastContactDate = reader.GetSafeNullableDateTime("LastContactDate"),
                    TotalChats = reader.GetSafeInt32("TotalChats"),
                    ServicesRequested = reader.GetSafeString("ServicesRequested", "")
                },
                p1
            );
        }
    }
}
