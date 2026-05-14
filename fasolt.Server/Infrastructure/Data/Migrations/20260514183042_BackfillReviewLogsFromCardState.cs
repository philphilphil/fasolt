using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// One-time backfill of synthetic ReviewLog rows for cards that were imported
    /// or created before review-log tracking was wired up. We can recover at least
    /// the minimum number of reviews implied by each card's current FSRS state:
    ///
    ///   - new        → 0 logs (never reviewed)
    ///   - learning   → 1 log at LastReviewedAt
    ///   - relearning → 1 log at LastReviewedAt
    ///   - review     → 2 logs: one at CreatedAt (graduation proxy) + one at LastReviewedAt
    ///
    /// Synthetic logs are marked by ScheduledDueAfter IS NULL (real reviews always set it).
    /// Idempotent: only touches cards that have no existing logs.
    /// </summary>
    public partial class BackfillReviewLogsFromCardState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pass 1: one synthetic log per non-new card with no existing logs,
            // dated at LastReviewedAt (or CreatedAt if LastReviewedAt is null).
            migrationBuilder.Sql(@"
                INSERT INTO ""ReviewLogs""
                    (""UserId"", ""CardId"", ""Rating"", ""ReviewedAt"", ""ScheduledDueAfter"", ""StateAfter"")
                SELECT
                    c.""UserId"",
                    c.""Id"",
                    'good',
                    COALESCE(c.""LastReviewedAt"", c.""CreatedAt""),
                    NULL,
                    c.""State""
                FROM ""Cards"" c
                WHERE c.""State"" <> 'new'
                  AND NOT EXISTS (SELECT 1 FROM ""ReviewLogs"" r WHERE r.""CardId"" = c.""Id"");
            ");

            // Pass 2: for review/relearning cards we just inserted into, add a second log
            // at CreatedAt as a stand-in for the initial graduation review.
            // Only runs if exactly one log exists for the card and it's synthetic — so it
            // won't double-fire if the migration is reapplied.
            migrationBuilder.Sql(@"
                INSERT INTO ""ReviewLogs""
                    (""UserId"", ""CardId"", ""Rating"", ""ReviewedAt"", ""ScheduledDueAfter"", ""StateAfter"")
                SELECT
                    c.""UserId"",
                    c.""Id"",
                    'good',
                    c.""CreatedAt"",
                    NULL,
                    'review'
                FROM ""Cards"" c
                WHERE c.""State"" IN ('review', 'relearning')
                  AND c.""LastReviewedAt"" IS NOT NULL
                  AND c.""LastReviewedAt"" > c.""CreatedAt""
                  AND (SELECT COUNT(*) FROM ""ReviewLogs"" r WHERE r.""CardId"" = c.""Id"") = 1
                  AND EXISTS (
                      SELECT 1 FROM ""ReviewLogs"" r
                      WHERE r.""CardId"" = c.""Id"" AND r.""ScheduledDueAfter"" IS NULL
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: a blanket "delete synthetic rows" rollback risks destroying
            // real review history if anything else ever writes a null ScheduledDueAfter.
            // Roll back manually if ever needed.
        }
    }
}
