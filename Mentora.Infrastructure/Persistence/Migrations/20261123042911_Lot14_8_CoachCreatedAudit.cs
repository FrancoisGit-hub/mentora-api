using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mentora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lot14_8_CoachCreatedAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lot 14.8a (14.G11): the admin coach creation writes action COACH_CREATED on entity type COACH.
            // Widening a CHECK cannot fail on existing rows, so there is no precheck. The CHECKs are raw SQL:
            // the model snapshot does not change.
            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION"
                CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED', 'EXERCISE_VIDEO_UPLOAD_CREATED', 'EXERCISE_VIDEO_REMOVED', 'COACH_CREATED'));
                """);

            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE"
                CHECK ("ADMIN_AUDIT_LOG_ENTITY_TYPE" IN ('EXERCISE', 'COACH'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse clearly (nothing changed yet) when rows the previous schema cannot hold exist.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "ADMIN_AUDIT_LOG"
                               WHERE "ADMIN_AUDIT_LOG_ACTION" = 'COACH_CREATED'
                                  OR "ADMIN_AUDIT_LOG_ENTITY_TYPE" = 'COACH') THEN
                        RAISE EXCEPTION 'Lot14_8_CoachCreatedAudit Down refused: ADMIN_AUDIT_LOG holds COACH rows. Remove them first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ENTITY_TYPE"
                CHECK ("ADMIN_AUDIT_LOG_ENTITY_TYPE" IN ('EXERCISE'));
                """);

            migrationBuilder.Sql("""ALTER TABLE "ADMIN_AUDIT_LOG" DROP CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION";""");
            migrationBuilder.Sql(
                """
                ALTER TABLE "ADMIN_AUDIT_LOG"
                ADD CONSTRAINT "CK_ADMIN_AUDIT_LOG_ACTION"
                CHECK ("ADMIN_AUDIT_LOG_ACTION" IN ('EXERCISE_CREATED', 'EXERCISE_UPDATED', 'EXERCISE_ACTIVATED', 'EXERCISE_DEACTIVATED', 'EXERCISE_VIDEO_UPLOAD_CREATED', 'EXERCISE_VIDEO_REMOVED'));
                """);
        }
    }
}
