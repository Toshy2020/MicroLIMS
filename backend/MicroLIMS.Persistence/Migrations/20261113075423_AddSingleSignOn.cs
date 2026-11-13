using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleSignOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DirectorySignInRefusedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorySubjectId",
                table: "Users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectoryUsername",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmergencyAccount",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SignInMethod",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SigningPasswordSetAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "LoginHistories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SsoExchangeCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoExchangeCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SsoExchangeCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SsoPendingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Nonce = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CodeVerifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SsoPendingRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DirectorySubjectId",
                table: "Users",
                column: "DirectorySubjectId",
                unique: true,
                filter: "\"DirectorySubjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DirectoryUsername",
                table: "Users",
                column: "DirectoryUsername",
                unique: true,
                filter: "\"DirectoryUsername\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmergencyAccount_MustBeLocal",
                table: "Users",
                sql: "(NOT \"IsEmergencyAccount\") OR (\"SignInMethod\" = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_SsoExchangeCodes_CodeHash",
                table: "SsoExchangeCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SsoExchangeCodes_ExpiresAt",
                table: "SsoExchangeCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SsoExchangeCodes_UserId",
                table: "SsoExchangeCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SsoPendingRequests_ExpiresAt",
                table: "SsoPendingRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_SsoPendingRequests_StateHash",
                table: "SsoPendingRequests",
                column: "StateHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SsoExchangeCodes");

            migrationBuilder.DropTable(
                name: "SsoPendingRequests");

            migrationBuilder.DropIndex(
                name: "IX_Users_DirectorySubjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DirectoryUsername",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmergencyAccount_MustBeLocal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DirectorySignInRefusedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DirectorySubjectId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DirectoryUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsEmergencyAccount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SignInMethod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SigningPasswordSetAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "LoginHistories");
        }
    }
}
