using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectQualityExtensionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_quality_extension_request_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Quality extension request % cannot be deleted.', OLD."Id";
                    END IF;
                    IF OLD."Decision" IS NOT NULL AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'Quality extension request % is already decided and immutable.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_qualityextensionrequests_immutable
                BEFORE UPDATE OR DELETE ON "QualityExtensionRequests"
                FOR EACH ROW EXECUTE FUNCTION prevent_quality_extension_request_modification();

                CREATE TRIGGER trg_qualityextensionrequests_no_truncate
                BEFORE TRUNCATE ON "QualityExtensionRequests"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE FUNCTION prevent_quality_record_link_core_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "QualityRecordSteps" WHERE "RecordId" = OLD."RecordId")
                       AND (NEW."RecordId" IS DISTINCT FROM OLD."RecordId"
                            OR NEW."TargetType" IS DISTINCT FROM OLD."TargetType"
                            OR NEW."TargetId" IS DISTINCT FROM OLD."TargetId"
                            OR NEW."NewRecordCode" IS DISTINCT FROM OLD."NewRecordCode") THEN
                        RAISE EXCEPTION 'Quality record link % core target columns cannot be modified after a step has been signed.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_qualityrecordlinks_columns_immutable
                BEFORE UPDATE ON "QualityRecordLinks"
                FOR EACH ROW EXECUTE FUNCTION prevent_quality_record_link_core_modification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_qualityrecordlinks_columns_immutable ON "QualityRecordLinks";
                DROP FUNCTION IF EXISTS prevent_quality_record_link_core_modification();
                DROP TRIGGER IF EXISTS trg_qualityextensionrequests_no_truncate ON "QualityExtensionRequests";
                DROP TRIGGER IF EXISTS trg_qualityextensionrequests_immutable ON "QualityExtensionRequests";
                DROP FUNCTION IF EXISTS prevent_quality_extension_request_modification();
                """);
        }
    }
}
