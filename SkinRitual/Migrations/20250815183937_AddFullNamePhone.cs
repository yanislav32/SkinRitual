using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkinRitual.Migrations
{
    /// <inheritdoc />
    public partial class AddFullNamePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Безопасно добавляем колонки, если их ещё нет (PostgreSQL)
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name   = 'Users'
      AND column_name  = 'FullName'
  ) THEN
    ALTER TABLE ""Users"" ADD COLUMN ""FullName"" text NULL;
  END IF;

  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name   = 'Users'
      AND column_name  = 'Phone'
  ) THEN
    ALTER TABLE ""Users"" ADD COLUMN ""Phone"" text NULL;
  END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""FullName"";
ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""Phone"";");
        }
    }
}
