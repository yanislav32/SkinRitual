using Microsoft.EntityFrameworkCore;
using SkinRitual.State.Models;

namespace SkinRitual.Data
{
    public class BotDbContext : DbContext
    {
        public BotDbContext(DbContextOptions<BotDbContext> opts) : base(opts) { }

        public DbSet<UserRecord> Users { get; set; } = null!;
        public DbSet<AnswerRecord> Answers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // 1) фиксируем схему
            mb.HasDefaultSchema("sr");

            // 2) Users
            mb.Entity<UserRecord>(b =>
            {
                b.ToTable("Users");

                // PK = реальный Telegram ChatId → НЕЛЬЗЯ автогенерировать в БД
                b.HasKey(u => u.ChatId);
                b.Property(u => u.ChatId).ValueGeneratedNever();

                // Столбцы
                b.Property(u => u.UserName).HasMaxLength(64);   // по желанию, чтобы не росло бесконечно
                b.Property(u => u.Phone).HasMaxLength(32);
                b.Property(u => u.FullName);                    // text ок
                b.Property(u => u.FirstSeen)
                 .IsRequired()
                 .HasColumnType("timestamp with time zone")
                 .HasDefaultValueSql("now()");                 // или "timezone('utc', now())"

                // Индексы (опционально):
                b.HasIndex(u => u.UserName);                   // поиск по нику
                b.HasIndex(u => u.Phone)
                 .HasDatabaseName("IX_Users_Phone_NotNull")
                 .HasFilter("\"Phone\" IS NOT NULL")
                 .IsUnique(false);                             // сделай true, если телефон должен быть уникален
            });

            // 3) Answers
            mb.Entity<AnswerRecord>(b =>
            {
                b.ToTable("Answers");

                b.HasKey(a => a.Id);
                b.Property(a => a.Id).ValueGeneratedOnAdd();

                b.Property(a => a.Response)
                 .IsRequired()
                 .HasColumnType("text");

                b.Property(a => a.AnsweredAt)
                 .IsRequired()
                 .HasColumnType("timestamp with time zone")
                 .HasDefaultValueSql("now()");

                b.Property(a => a.Step)
                 .IsRequired(); // enum → int (по умолчанию)

                // FK + индекс
                b.HasOne(a => a.User)
                 .WithMany(u => u.Answers)
                 .HasForeignKey(a => a.ChatId)
                 .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(a => a.ChatId);
            });
        }
    }
}
