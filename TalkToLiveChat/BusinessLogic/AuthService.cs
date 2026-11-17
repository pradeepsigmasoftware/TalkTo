using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.BusinessLogic
{
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string inputPassword, string storedHash);
        Task<Agent?> AuthenticateUserAsync(string email, string password);
    }

    public class AuthService : IAuthService
    {
        private readonly IAgentRepository _agentRepo;

        public AuthService(IAgentRepository agentRepo)
        {
            _agentRepo = agentRepo;
        }

        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        public bool VerifyPassword(string inputPassword, string storedHash)
        {
            string hashedInput = HashPassword(inputPassword);
            return string.Equals(hashedInput, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<Agent?> AuthenticateUserAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var user = await _agentRepo.GetAgentByEmailAsync(email.Trim());
            if (user == null || !user.IsActive)
            {
                return null;
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            return user;
        }
    }
}
