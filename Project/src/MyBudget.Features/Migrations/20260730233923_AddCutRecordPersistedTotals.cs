using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// CS-9: hand-edited per design.md Decision 6 into three phases so that no unmodelled
    /// DB default ever ships zeros silently:
    ///   A) add all 16 columns nullable (no defaultValue);
    ///   B) SQL backfill from CutBankAccounts aggregation + the execution-summary CTE
    ///      (re-expressed here — same logic as BudgetExecutionSummaryQuery/CutTotalsCalculator,
    ///      accepted duplication per design.md, dead code once the migration has run),
    ///      then zero-fill any row that still has no CutBankAccount rows to join against;
    ///   C) AlterColumn all 16 to non-nullable now that every row has a value.
    /// </remarks>
    public partial class AddCutRecordPersistedTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Phase A: add all 16 columns as nullable ───────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "TotalPositive", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPositiveAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNegative", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNegativeAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeudaEnCurso", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDeudaEnCursoAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalBudgeted", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalBudgetedAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRegistered", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRegisteredAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Remaining", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAvailable", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAvailableAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNet", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNetAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);

            // ── Phase B: backfill (CS-9) ───────────────────────────────────────
            // Bank-account aggregation ("a") + execution-summary CTE ("ex"), correlated
            // per-row via "src" (a "CutRecords" self-join anchor) — same logic as
            // BudgetExecutionSummaryQuery + CutTotalsCalculator, re-expressed in SQL.
            // Rows whose period has since closed correctly backfill the execution trio
            // to 0 (p."IsClosed" = false filter), matching pre-change GetCutRecord output.
            //
            // NOTE: correlation is against "src" (a plain FROM-list entry inside the derived
            // table "x"), never against the UPDATE target "cr" directly — Postgres rejects a
            // LATERAL subquery in an UPDATE ... FROM clause correlating to the target relation
            // itself ("42P10: invalid reference to FROM-clause entry for table cr"); LATERAL
            // may only reference other entries already present in the same FROM list.
            migrationBuilder.Sql("""
                UPDATE "CutRecords" cr
                SET
                    "TotalPositive"        = ROUND(x."Pos", 2),
                    "TotalPositiveAlt"     = ROUND(x."Pos" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalNegative"        = ROUND(x."Neg", 2),
                    "TotalNegativeAlt"     = ROUND(x."Neg" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalDeudaEnCurso"    = ROUND(x."Remaining" + x."Neg", 2),
                    "TotalDeudaEnCursoAlt" = ROUND((x."Remaining" + x."Neg") / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalBudgeted"        = ROUND(x."TotalBudgeted", 2),
                    "TotalBudgetedAlt"     = ROUND(x."TotalBudgeted" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalRegistered"      = ROUND(x."TotalRegistered", 2),
                    "TotalRegisteredAlt"   = ROUND(x."TotalRegistered" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "Remaining"            = ROUND(x."Remaining", 2),
                    "RemainingAlt"         = ROUND(x."Remaining" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalAvailable"       = ROUND(x."Pos", 2),
                    "TotalAvailableAlt"    = ROUND(x."Pos" / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2),
                    "TotalNet"             = ROUND(x."Pos" - (x."Remaining" + x."Neg"), 2),
                    "TotalNetAlt"          = ROUND((x."Pos" - (x."Remaining" + x."Neg")) / (CASE WHEN cr."ExchangeRate" > 0 THEN cr."ExchangeRate" ELSE 1 END), 2)
                FROM (
                    SELECT
                        src."Id" AS "CutRecordId",
                        a."Pos", a."Neg",
                        COALESCE(ex."TotalBudgeted", 0)   AS "TotalBudgeted",
                        COALESCE(ex."TotalRegistered", 0) AS "TotalRegistered",
                        COALESCE(ex."Remaining", 0)       AS "Remaining"
                    FROM "CutRecords" src
                    JOIN (
                        SELECT
                            cba."CutRecordId",
                            COALESCE(SUM(cba."BalanceInPrimary") FILTER (WHERE cba."IsPositive"), 0)     AS "Pos",
                            COALESCE(SUM(cba."BalanceInPrimary") FILTER (WHERE NOT cba."IsPositive"), 0) AS "Neg"
                        FROM "CutBankAccounts" cba
                        GROUP BY cba."CutRecordId"
                    ) a ON a."CutRecordId" = src."Id"
                    LEFT JOIN LATERAL (
                        WITH active_period AS (
                            SELECT
                                p."Id"                 AS "PeriodId",
                                p."StartDate"          AS "PeriodStart",
                                p."EndDate"            AS "PeriodEnd",
                                cy."DefaultCurrencyId" AS "DefaultCurrencyId"
                            FROM "Periods" p
                            JOIN "Cycles" cy ON cy."Id" = p."CycleId"
                            WHERE cy."BudgetId"  = src."BudgetId"
                              AND cy."DeletedAt" IS NULL
                              AND p."DeletedAt"  IS NULL
                              AND p."IsClosed"   = false
                              AND p."StartDate"  <= src."CutDate"
                              AND p."EndDate"    >= src."CutDate"
                            LIMIT 1
                        ),
                        budgeted AS (
                            SELECT COALESCE(SUM(rev."BudgetedAmount"), 0) AS "TotalBudgeted"
                            FROM "BudgetLines" bl
                            JOIN active_period ap ON true
                            LEFT JOIN LATERAL (
                                SELECT r."BudgetedAmount"
                                FROM "BudgetLineRevisions" r
                                WHERE r."BudgetLineId" = bl."Id"
                                  AND r."ValidFrom"::date <= ap."PeriodStart"
                                  AND (r."ValidTo" IS NULL OR r."ValidTo"::date >= ap."PeriodStart")
                                LIMIT 1
                            ) rev ON true
                            WHERE bl."BudgetId"  = src."BudgetId"
                              AND bl."DeletedAt" IS NULL
                              AND bl."StartDate"::date <= ap."PeriodEnd"
                              AND (bl."EndDate" IS NULL OR bl."EndDate"::date >= ap."PeriodStart")
                        ),
                        registered AS (
                            SELECT COALESCE(SUM(
                                CASE
                                    WHEN e."EntryType" = 1 THEN
                                        CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                            THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END
                                    WHEN e."EntryType" = 3 THEN
                                        CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                            THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END
                                    WHEN e."EntryType" = 2 THEN
                                        -(CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                                            THEN e."Amount" ELSE e."Amount" * e."ExchangeRate" END)
                                    ELSE 0
                                END
                            ), 0) AS "TotalRegistered"
                            FROM "ExecutionRecords" e
                            JOIN active_period ap ON ap."PeriodId" = e."PeriodId"
                            WHERE e."BudgetId"  = src."BudgetId"
                              AND e."DeletedAt" IS NULL
                        )
                        SELECT
                            b."TotalBudgeted",
                            r."TotalRegistered",
                            (b."TotalBudgeted" - r."TotalRegistered") AS "Remaining"
                        FROM budgeted b
                        CROSS JOIN registered r
                    ) ex ON true
                ) x
                WHERE cr."Id" = x."CutRecordId";

                -- Cuts with no CutBankAccounts rows never matched the join above; zero-fill them.
                UPDATE "CutRecords" SET
                    "TotalPositive" = 0, "TotalPositiveAlt" = 0,
                    "TotalNegative" = 0, "TotalNegativeAlt" = 0,
                    "TotalDeudaEnCurso" = 0, "TotalDeudaEnCursoAlt" = 0,
                    "TotalBudgeted" = 0, "TotalBudgetedAlt" = 0,
                    "TotalRegistered" = 0, "TotalRegisteredAlt" = 0,
                    "Remaining" = 0, "RemainingAlt" = 0,
                    "TotalAvailable" = 0, "TotalAvailableAlt" = 0,
                    "TotalNet" = 0, "TotalNetAlt" = 0
                WHERE "TotalPositive" IS NULL;
                """);

            // ── Phase C: enforce NOT NULL now that every row has a value ───────
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPositive", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPositiveAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNegative", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNegativeAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDeudaEnCurso", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalDeudaEnCursoAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalBudgeted", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalBudgetedAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalRegistered", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalRegisteredAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Remaining", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RemainingAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAvailable", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAvailableAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNet", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNetAlt", table: "CutRecords",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(18,2)", oldPrecision: 18, oldScale: 2, oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Remaining",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "RemainingAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalAvailable",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalAvailableAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalBudgeted",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalBudgetedAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalDeudaEnCurso",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalDeudaEnCursoAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalNegative",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalNegativeAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalNet",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalNetAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalPositive",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalPositiveAlt",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalRegistered",
                table: "CutRecords");

            migrationBuilder.DropColumn(
                name: "TotalRegisteredAlt",
                table: "CutRecords");
        }
    }
}
