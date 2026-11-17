using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IServiceRepository
    {
        Task<List<ServiceItem>> GetActiveServicesAsync();
        Task<List<ServiceItem>> GetAllServicesAsync();
        Task<ServiceItem?> GetServiceByIdAsync(int serviceId);
        Task<int> SaveServiceAsync(ServiceItem service);
        Task<int> ToggleServiceStatusAsync(int serviceId, bool isActive);
    }

    public class ServiceRepository : IServiceRepository
    {
        private readonly IDatabaseHelper _db;

        public ServiceRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<List<ServiceItem>> GetActiveServicesAsync()
        {
            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetActiveServices",
                CommandType.StoredProcedure,
                reader => new ServiceItem
                {
                    ServiceId = reader.GetSafeInt32("ServiceId"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    Description = reader.GetSafeString("Description", ""),
                    DisplayOrder = reader.GetSafeInt32("DisplayOrder"),
                    IsActive = reader.GetSafeBoolean("IsActive")
                }
            );
        }

        public async Task<List<ServiceItem>> GetAllServicesAsync()
        {
            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetAllServices",
                CommandType.StoredProcedure,
                reader => new ServiceItem
                {
                    ServiceId = reader.GetSafeInt32("ServiceId"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    Description = reader.GetSafeString("Description", ""),
                    DisplayOrder = reader.GetSafeInt32("DisplayOrder"),
                    IsActive = reader.GetSafeBoolean("IsActive"),
                    AssignedAgentsCount = reader.GetSafeInt32("AssignedAgentsCount"),
                    TotalSessionsCount = reader.GetSafeInt32("TotalSessionsCount")
                }
            );
        }

        public async Task<ServiceItem?> GetServiceByIdAsync(int serviceId)
        {
            var p1 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = serviceId };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_GetServiceById",
                CommandType.StoredProcedure,
                reader => new ServiceItem
                {
                    ServiceId = reader.GetSafeInt32("ServiceId"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    Description = reader.GetSafeString("Description", ""),
                    DisplayOrder = reader.GetSafeInt32("DisplayOrder"),
                    IsActive = reader.GetSafeBoolean("IsActive")
                },
                p1
            );
        }

        public async Task<int> SaveServiceAsync(ServiceItem service)
        {
            var p1 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = service.ServiceId };
            var p2 = new SqlParameter("@ServiceName", SqlDbType.NVarChar, 100) { Value = service.ServiceName };
            var p3 = new SqlParameter("@Description", SqlDbType.NVarChar, 500) { Value = (object?)service.Description ?? DBNull.Value };
            var p4 = new SqlParameter("@DisplayOrder", SqlDbType.Int) { Value = service.DisplayOrder };
            var p5 = new SqlParameter("@IsActive", SqlDbType.Bit) { Value = service.IsActive };

            var result = await _db.ExecuteScalarAsync(
                "dbo.sp_SaveService",
                CommandType.StoredProcedure,
                p1, p2, p3, p4, p5
            );

            return Convert.ToInt32(result);
        }

        public async Task<int> ToggleServiceStatusAsync(int serviceId, bool isActive)
        {
            var p1 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = serviceId };
            var p2 = new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive };

            var result = await _db.ExecuteScalarAsync(
                "dbo.sp_DeleteOrToggleService",
                CommandType.StoredProcedure,
                p1, p2
            );

            return Convert.ToInt32(result);
        }
    }
}
