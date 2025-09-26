using Microsoft.EntityFrameworkCore;
using SkinRitual.State.Models;

namespace SkinRitual.Data
{
    public class BotDbContext : DbContext
    {
        public BotDbContext(DbContextOptions<BotDbContext> opts) : base(opts) { }

        public DbSet<UserRecord> Users { get; set; }
        public DbSet<AnswerRecord> Answers { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<UserRecord>()
              .HasKey(u => u.ChatId);

            mb.Entity<AnswerRecord>()
              .HasKey(a => a.Id);
            mb.Entity<AnswerRecord>()
              .HasOne(a => a.User)
              .WithMany(u => u.Answers)
              .HasForeignKey(a => a.ChatId);
        }
    }
}
