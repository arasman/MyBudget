using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetIdToChildEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "Periods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "BudgetLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetId",
                table: "BudgetLineRevisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Periods_BudgetId",
                table: "Periods",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BudgetId",
                table: "Categories",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetId",
                table: "BudgetLines",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLineRevisions_BudgetId",
                table: "BudgetLineRevisions",
                column: "BudgetId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLineRevisions_Budgets_BudgetId",
                table: "BudgetLineRevisions",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLines_Budgets_BudgetId",
                table: "BudgetLines",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Budgets_BudgetId",
                table: "Categories",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Periods_Budgets_BudgetId",
                table: "Periods",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLineRevisions_Budgets_BudgetId",
                table: "BudgetLineRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLines_Budgets_BudgetId",
                table: "BudgetLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Budgets_BudgetId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Periods_Budgets_BudgetId",
                table: "Periods");

            migrationBuilder.DropIndex(
                name: "IX_Periods_BudgetId",
                table: "Periods");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BudgetId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLines_BudgetId",
                table: "BudgetLines");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLineRevisions_BudgetId",
                table: "BudgetLineRevisions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Periods");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "BudgetLineRevisions");
        }
    }
}
