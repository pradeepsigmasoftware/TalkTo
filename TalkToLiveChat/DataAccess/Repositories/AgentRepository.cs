using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IAgentRepository
    {
        Task<List<Agent>> GetAllAgentsAsync();
        Task<Agent?> GetAgentByIdAsync(int agentId);
        Task<Agent?> GetAgentByEmailAsync(string email);
        Task<int> SaveAgentAsync(Agent agent);
        Task UpdateAvailabilityAsync(int agentId, string status);
        Task SetAgentServicesAsync(int agentId, List<int> serviceIds);
        Task<List<Agent>> GetAvailableAgentsForTransferAsync(int currentAgentId);
        Task<Agent?> FindAvailableAgentForServiceAsync(int serviceId);
        Task<AgentDashboardStats> GetAgentDashboardStatsAsync(int agentId);
        Task<AdminDashboardStats> GetAdminDashboardStatsAsync();
    }

    public class AgentRepository : IAgentRepository
    {
        private readonly IDatabaseHelper _db;

        public AgentRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<List<Agent>> GetAllAgentsAsync()
        {
            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetAllAgents",
                CommandType.StoredProcedure,
                reader => new Agent
                {
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    Email = reader.GetSafeString("Email"),
                    ProfileImage = reader.GetSafeString("ProfileImage", "/images/default-avatar.png"),
                    AvailabilityStatus = reader.GetSafeString("AvailabilityStatus", "Offline"),
                    MaxConcurrentChats = reader.GetSafeInt32("MaxConcurrentChats", 5),
                    Role = reader.GetSafeString("Role", "Agent"),
                    IsActive = reader.GetSafeBoolean("IsActive", true),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate"),
                    ActiveChatsCount = reader.GetSafeInt32("ActiveChatsCount"),
                    TotalAssignedChats = reader.GetSafeInt32("TotalAssignedChats"),
                    AssignedServices = reader.GetSafeString("AssignedServices", "")
                }
            );
        }

        public async Task<Agent?> GetAgentByIdAsync(int agentId)
        {
            Agent? agent = null;
            var serviceIds = new List<int>();

            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };

            await _db.ExecuteReaderMultiAsync(
                "dbo.sp_GetAgentById",
                CommandType.StoredProcedure,
                reader =>
                {
                    if (reader.Read())
                    {
                        agent = new Agent
                        {
                            AgentId = reader.GetSafeInt32("AgentId"),
                            AgentName = reader.GetSafeString("AgentName"),
                            Email = reader.GetSafeString("Email"),
                            PasswordHash = reader.GetSafeString("PasswordHash"),
                            ProfileImage = reader.GetSafeString("ProfileImage", "/images/default-avatar.png"),
                            AvailabilityStatus = reader.GetSafeString("AvailabilityStatus", "Offline"),
                            MaxConcurrentChats = reader.GetSafeInt32("MaxConcurrentChats", 5),
                            Role = reader.GetSafeString("Role", "Agent"),
                            IsActive = reader.GetSafeBoolean("IsActive", true),
                            CreatedDate = reader.GetSafeDateTime("CreatedDate")
                        };
                    }

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            serviceIds.Add(reader.GetInt32(0));
                        }
                    }
                },
                p1
            );

            if (agent != null)
            {
                agent.AssignedServiceIds = serviceIds;
            }

            return agent;
        }

        public async Task<Agent?> GetAgentByEmailAsync(string email)
        {
            var p1 = new SqlParameter("@Email", SqlDbType.NVarChar, 150) { Value = email };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_GetAgentByEmail",
                CommandType.StoredProcedure,
                reader => new Agent
                {
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    Email = reader.GetSafeString("Email"),
                    PasswordHash = reader.GetSafeString("PasswordHash"),
                    ProfileImage = reader.GetSafeString("ProfileImage", "/images/default-avatar.png"),
                    AvailabilityStatus = reader.GetSafeString("AvailabilityStatus", "Offline"),
                    MaxConcurrentChats = reader.GetSafeInt32("MaxConcurrentChats", 5),
                    Role = reader.GetSafeString("Role", "Agent"),
                    IsActive = reader.GetSafeBoolean("IsActive", true),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate")
                },
                p1
            );
        }

        public async Task<int> SaveAgentAsync(Agent agent)
        {
            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agent.AgentId };
            var p2 = new SqlParameter("@AgentName", SqlDbType.NVarChar, 100) { Value = agent.AgentName };
            var p3 = new SqlParameter("@Email", SqlDbType.NVarChar, 150) { Value = agent.Email };
            var p4 = new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 255) { Value = (object?)agent.PasswordHash ?? DBNull.Value };
            var p5 = new SqlParameter("@ProfileImage", SqlDbType.NVarChar, 255) { Value = (object?)agent.ProfileImage ?? DBNull.Value };
            var p6 = new SqlParameter("@MaxConcurrentChats", SqlDbType.Int) { Value = agent.MaxConcurrentChats };
            var p7 = new SqlParameter("@Role", SqlDbType.NVarChar, 20) { Value = agent.Role };
            var p8 = new SqlParameter("@IsActive", SqlDbType.Bit) { Value = agent.IsActive };

            var result = await _db.ExecuteScalarAsync(
                "dbo.sp_SaveAgent",
                CommandType.StoredProcedure,
                p1, p2, p3, p4, p5, p6, p7, p8
            );

            return Convert.ToInt32(result);
        }

        public async Task UpdateAvailabilityAsync(int agentId, string status)
        {
            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };
            var p2 = new SqlParameter("@AvailabilityStatus", SqlDbType.NVarChar, 20) { Value = status };

            await _db.ExecuteNonQueryAsync(
                "dbo.sp_UpdateAgentAvailability",
                CommandType.StoredProcedure,
                p1, p2
            );
        }

        public async Task SetAgentServicesAsync(int agentId, List<int> serviceIds)
        {
            string idsStr = string.Join(",", serviceIds);
            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };
            var p2 = new SqlParameter("@ServiceIds", SqlDbType.NVarChar, -1) { Value = idsStr };

            await _db.ExecuteNonQueryAsync(
                "dbo.sp_SetAgentServices",
                CommandType.StoredProcedure,
                p1, p2
            );
        }

        public async Task<List<Agent>> GetAvailableAgentsForTransferAsync(int currentAgentId)
        {
            var p1 = new SqlParameter("@CurrentAgentId", SqlDbType.Int) { Value = currentAgentId };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetAvailableAgentsForTransfer",
                CommandType.StoredProcedure,
                reader => new Agent
                {
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    Email = reader.GetSafeString("Email"),
                    AvailabilityStatus = reader.GetSafeString("AvailabilityStatus"),
                    MaxConcurrentChats = reader.GetSafeInt32("MaxConcurrentChats"),
                    ActiveChatsCount = reader.GetSafeInt32("ActiveChats")
                },
                p1
            );
        }

        public async Task<Agent?> FindAvailableAgentForServiceAsync(int serviceId)
        {
            var p1 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = serviceId };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_FindAvailableAgentForService",
                CommandType.StoredProcedure,
                reader => new Agent
                {
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    Email = reader.GetSafeString("Email"),
                    ProfileImage = reader.GetSafeString("ProfileImage", "/images/default-avatar.png"),
                    AvailabilityStatus = reader.GetSafeString("AvailabilityStatus"),
                    MaxConcurrentChats = reader.GetSafeInt32("MaxConcurrentChats"),
                    Role = reader.GetSafeString("Role"),
                    ActiveChatsCount = reader.GetSafeInt32("CurrentActiveChats")
                },
                p1
            );
        }

        public async Task<AgentDashboardStats> GetAgentDashboardStatsAsync(int agentId)
        {
            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };

            var stats = await _db.ExecuteReaderSingleAsync(
                "dbo.sp_GetAgentDashboardStats",
                CommandType.StoredProcedure,
                reader => new AgentDashboardStats
                {
                    TotalAssignedChats = reader.GetSafeInt32("TotalAssignedChats"),
                    ActiveChats = reader.GetSafeInt32("ActiveChats"),
                    PendingChats = reader.GetSafeInt32("PendingChats"),
                    CompletedChats = reader.GetSafeInt32("CompletedChats"),
                    UnreadMessages = reader.GetSafeInt32("UnreadMessages")
                },
                p1
            );

            return stats ?? new AgentDashboardStats();
        }

        public async Task<AdminDashboardStats> GetAdminDashboardStatsAsync()
        {
            var stats = new AdminDashboardStats();

            await _db.ExecuteReaderMultiAsync(
                "dbo.sp_GetAdminDashboardStats",
                CommandType.StoredProcedure,
                reader =>
                {
                    if (reader.Read())
                    {
                        stats.TotalVisitors = reader.GetSafeInt32("TotalVisitors");
                        stats.TotalChats = reader.GetSafeInt32("TotalChats");
                        stats.ActiveChats = reader.GetSafeInt32("ActiveChats");
                        stats.PendingChats = reader.GetSafeInt32("PendingChats");
                        stats.CompletedChats = reader.GetSafeInt32("CompletedChats");
                        stats.OnlineAgents = reader.GetSafeInt32("OnlineAgents");
                        stats.OfflineAgents = reader.GetSafeInt32("OfflineAgents");
                    }

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            stats.ServiceStats.Add(new ServiceWiseStats
                            {
                                ServiceId = reader.GetSafeInt32("ServiceId"),
                                ServiceName = reader.GetSafeString("ServiceName"),
                                TotalChats = reader.GetSafeInt32("TotalChats"),
                                ActiveChats = reader.GetSafeInt32("ActiveChats"),
                                PendingChats = reader.GetSafeInt32("PendingChats"),
                                CompletedChats = reader.GetSafeInt32("CompletedChats")
                            });
                        }
                    }
                }
            );

            return stats;
        }
    }
}
