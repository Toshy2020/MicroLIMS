using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding: creates the MediaProducts master table,
    // adds MediaProductId as nullable first, backfills distinct product groups
    // and candidate codes from MediaConfigurations and DehydratedMedia Materials,
    // tightens MediaConfigurations.MediaProductId to NOT NULL, swaps the unique
    // profile index from Name to MediaProductId, creates FKs and indexes, and
    // creates case-insensitive expression indexes on MediaProducts (Name, Code).
    public partial class AddMediaProductMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- a. Create table MediaProducts ----
            migrationBuilder.CreateTable(
                name: "MediaProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaProducts", x => x.Id);
                });

            // ---- b. Add MediaProductId columns as nullable first ----
            migrationBuilder.AddColumn<int>(
                name: "MediaProductId",
                table: "MediaConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaProductId",
                table: "Materials",
                type: "integer",
                nullable: true);

            // ---- c. Backfill MediaProducts and assign MediaProductId ----
            // DehydratedMedia = 0 (MicroLIMS.Domain.Enums.MaterialType.DehydratedMedia).
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    rec RECORD;
                    v_product_id integer;
                    v_product_name text;
                    v_raw_code text;
                    v_cand_code text;
                    v_final_code text;
                    v_n integer;
                BEGIN
                    FOR rec IN
                        WITH cfg_groups AS (
                            SELECT lower(btrim(""Name"")) AS norm_name,
                                   min(""Id"") AS min_cfg_id
                            FROM ""MediaConfigurations""
                            GROUP BY lower(btrim(""Name""))
                        ),
                        mat_groups AS (
                            SELECT lower(btrim(""MaterialName"")) AS norm_name,
                                   min(""Id"") AS min_mat_id
                            FROM ""Materials""
                            WHERE ""MaterialType"" = 0
                            GROUP BY lower(btrim(""MaterialName""))
                        ),
                        all_groups AS (
                            SELECT norm_name FROM cfg_groups
                            UNION
                            SELECT norm_name FROM mat_groups
                        )
                        SELECT g.norm_name,
                               c.min_cfg_id,
                               m.min_mat_id
                        FROM all_groups g
                        LEFT JOIN cfg_groups c ON c.norm_name = g.norm_name
                        LEFT JOIN mat_groups m ON m.norm_name = g.norm_name
                        ORDER BY
                            CASE WHEN c.min_cfg_id IS NOT NULL THEN 0 ELSE 1 END ASC,
                            c.min_cfg_id ASC,
                            m.min_mat_id ASC
                    LOOP
                        -- Product Name = btrim of the MediaConfigurations.""Name"" with the lowest ""Id"" in the group;
                        -- if the group has no configuration, btrim of the MaterialName on the lowest Materials.""Id"".
                        IF rec.min_cfg_id IS NOT NULL THEN
                            SELECT btrim(""Name"") INTO v_product_name
                            FROM ""MediaConfigurations""
                            WHERE ""Id"" = rec.min_cfg_id;
                        ELSE
                            SELECT btrim(""MaterialName"") INTO v_product_name
                            FROM ""Materials""
                            WHERE ""Id"" = rec.min_mat_id;
                        END IF;

                        -- Product Code candidate = the most frequent non-blank btrim(Materials.""Code"")
                        -- among that group's DehydratedMedia materials (tie -> the code on the lowest Material Id),
                        -- with every character outside [A-Za-z0-9.-] removed, truncated to 10.
                        SELECT btrim(""Code"") INTO v_raw_code
                        FROM ""Materials""
                        WHERE ""MaterialType"" = 0
                          AND lower(btrim(""MaterialName"")) = rec.norm_name
                          AND ""Code"" IS NOT NULL
                          AND btrim(""Code"") <> ''
                        GROUP BY btrim(""Code"")
                        ORDER BY count(*) DESC, min(""Id"") ASC
                        LIMIT 1;

                        v_cand_code := NULL;
                        IF v_raw_code IS NOT NULL THEN
                            v_cand_code := left(regexp_replace(v_raw_code, '[^A-Za-z0-9.-]', '', 'g'), 10);
                        END IF;

                        -- If there is no such code, or the result is shorter than 2 characters:
                        -- upper(regexp_replace(ProductName, '[^A-Za-z0-9]', '', 'g')) truncated to 10, right-padded with 'X' to at least 2.
                        IF v_cand_code IS NULL OR length(v_cand_code) < 2 THEN
                            v_cand_code := left(upper(regexp_replace(v_product_name, '[^A-Za-z0-9]', '', 'g')), 10);
                            IF length(v_cand_code) < 2 THEN
                                v_cand_code := rpad(v_cand_code, 2, 'X');
                            END IF;
                        END IF;

                        -- If a candidate code is already used (case-insensitive) by an earlier group,
                        -- use left(code, 8) || lpad(n::text, 2, '0') with n = 1, 2, ... until free.
                        v_final_code := v_cand_code;
                        v_n := 1;
                        WHILE EXISTS (SELECT 1 FROM ""MediaProducts"" WHERE lower(""Code"") = lower(v_final_code)) LOOP
                            v_final_code := left(v_cand_code, 8) || lpad(v_n::text, 2, '0');
                            v_n := v_n + 1;
                        END LOOP;

                        -- Insert into MediaProducts
                        INSERT INTO ""MediaProducts"" (""Name"", ""Code"")
                        VALUES (v_product_name, v_final_code)
                        RETURNING ""Id"" INTO v_product_id;

                        -- UPDATE MediaConfigurations: set MediaProductId and sync Name to product Name
                        UPDATE ""MediaConfigurations""
                        SET ""MediaProductId"" = v_product_id,
                            ""Name"" = v_product_name
                        WHERE lower(btrim(""Name"")) = rec.norm_name;

                        -- UPDATE Materials: set MediaProductId for DehydratedMedia materials
                        UPDATE ""Materials""
                        SET ""MediaProductId"" = v_product_id
                        WHERE ""MaterialType"" = 0
                          AND lower(btrim(""MaterialName"")) = rec.norm_name;

                    END LOOP;

                    IF EXISTS (SELECT 1 FROM ""MediaConfigurations"" WHERE ""MediaProductId"" IS NULL) THEN
                        RAISE EXCEPTION 'MediaConfigurations has rows with unresolved MediaProductId after backfill';
                    END IF;
                END $$;
            ");

            // ---- d. Alter MediaConfigurations.MediaProductId to NOT NULL ----
            migrationBuilder.AlterColumn<int>(
                name: "MediaProductId",
                table: "MediaConfigurations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // ---- e. Indexes + FKs ----
            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_Name_IncubationMinHours_IncubationMaxHo~",
                table: "MediaConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_MediaProductId_IncubationMinHours_Incub~",
                table: "MediaConfigurations",
                columns: new[] { "MediaProductId", "IncubationMinHours", "IncubationMaxHours", "TemperatureMin", "TemperatureMax" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MediaProductId",
                table: "Materials",
                column: "MediaProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_MediaProducts_MediaProductId",
                table: "Materials",
                column: "MediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaConfigurations_MediaProducts_MediaProductId",
                table: "MediaConfigurations",
                column: "MediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- f. Case-insensitive expression indexes on MediaProducts ----
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_MediaProducts_Name_Lower\" ON \"MediaProducts\" (lower(\"Name\"));");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_MediaProducts_Code_Lower\" ON \"MediaProducts\" (lower(\"Code\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop expression indexes on MediaProducts
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MediaProducts_Code_Lower\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MediaProducts_Name_Lower\";");

            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_MediaProducts_MediaProductId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaConfigurations_MediaProducts_MediaProductId",
                table: "MediaConfigurations");

            // Drop new indexes
            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_MediaProductId_IncubationMinHours_Incub~",
                table: "MediaConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_Materials_MediaProductId",
                table: "Materials");

            // Drop MediaProductId columns
            migrationBuilder.DropColumn(
                name: "MediaProductId",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "MediaProductId",
                table: "Materials");

            // Drop MediaProducts table
            migrationBuilder.DropTable(
                name: "MediaProducts");

            // Recreate old unique index on MediaConfigurations (Name + profile)
            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_Name_IncubationMinHours_IncubationMaxHo~",
                table: "MediaConfigurations",
                columns: new[] { "Name", "IncubationMinHours", "IncubationMaxHours", "TemperatureMin", "TemperatureMax" },
                unique: true);
        }
    }
}
