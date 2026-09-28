using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    // Maps PostgreSQL's xmin system column as an optimistic-concurrency
    // token on every table (see MicroLimsDbContext.OnModelCreating). xmin
    // exists on every PostgreSQL row already and Npgsql never emits DDL for
    // it, so this migration changes nothing in the database - it only
    // records the token in the model snapshot. The generated AddColumn /
    // DropColumn calls for xmin rendered to no SQL and are omitted.
    /// <inheritdoc />
    public partial class AddXminConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
