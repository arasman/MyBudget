using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetLineCurrencyAndDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Purge all existing BudgetLineRevision rows (test data — approved)
            migrationBuilder.Sql(@"DELETE FROM ""BudgetLineRevisions""");

            // Step 2: Drop the old Currency varchar column
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BudgetLineRevisions");

            // Step 3: Add CurrencyId uuid NOT NULL with temporary default so the column can be created
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "BudgetLineRevisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Step 4: Remove the default (column must be NOT NULL without a default after ADD)
            migrationBuilder.AlterColumn<Guid>(
                name: "CurrencyId",
                table: "BudgetLineRevisions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: false,
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLineRevisions_CurrencyId",
                table: "BudgetLineRevisions",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLineRevisions_Currencies_CurrencyId",
                table: "BudgetLineRevisions",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Step 5: Add DisplayOrder int NOT NULL DEFAULT 0 on BudgetLines
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "BudgetLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 6: Backfill DisplayOrder using ROW_NUMBER() within (PeriodId, CategoryGroupId, CategoryId)
            migrationBuilder.Sql(
                @"UPDATE ""BudgetLines""
                  SET ""DisplayOrder"" = sub.rn
                  FROM (
                      SELECT ""Id"", ROW_NUMBER() OVER (
                          PARTITION BY ""PeriodId"", ""CategoryGroupId"", ""CategoryId""
                          ORDER BY ""CreatedAt""
                      ) AS rn
                      FROM ""BudgetLines""
                  ) sub
                  WHERE ""BudgetLines"".""Id"" = sub.""Id""");

            // Step 7: Remove the DEFAULT 0 from DisplayOrder
            migrationBuilder.AlterColumn<int>(
                name: "DisplayOrder",
                table: "BudgetLines",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false,
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLineRevisions_Currencies_CurrencyId",
                table: "BudgetLineRevisions");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLineRevisions_CurrencyId",
                table: "BudgetLineRevisions");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "BudgetLineRevisions");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "BudgetLineRevisions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "GTQ");
        }
    }
}
