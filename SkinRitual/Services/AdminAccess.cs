using Microsoft.Extensions.Configuration;

namespace SkinRitual.Services
{
    public sealed class AdminAccess
    {
        public HashSet<long> Ids { get; }

        public AdminAccess(IConfiguration cfg)
        {
            // читаем именно AdminChatIds; оставляем бэкап-названия на всякий
            var raw = cfg["AdminChatIds"]
                   ?? cfg["ADMINS"]
                   ?? cfg["ADMIN_IDS"]
                   ?? string.Empty;

            Ids = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => long.TryParse(s.Trim(), out var id) ? id : (long?)null)
                     .Where(id => id.HasValue)
                     .Select(id => id!.Value)
                     .ToHashSet();
        }

        public bool IsAdmin(long userId) => Ids.Contains(userId);
    }
}
