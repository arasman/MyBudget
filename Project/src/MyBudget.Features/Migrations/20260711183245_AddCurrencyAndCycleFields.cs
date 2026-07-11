using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndCycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create Currencies table first (FK target)
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            // Step 2: Insert seed rows so FK default value is valid
            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "Name", "Symbol" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "GTQ", "Quetzal", "Q" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "USD", "US Dollar", "$" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "EUR", "Euro", "€" }
                });

            // Step 3: Add currency columns to Cycles (DefaultCurrencyId defaults to GTQ)
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCurrencyId",
                table: "Cycles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "AlternateCurrencyId",
                table: "Cycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Cycles",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cycles_AlternateCurrencyId",
                table: "Cycles",
                column: "AlternateCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Cycles_DefaultCurrencyId",
                table: "Cycles",
                column: "DefaultCurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cycles_Currencies_AlternateCurrencyId",
                table: "Cycles",
                column: "AlternateCurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cycles_Currencies_DefaultCurrencyId",
                table: "Cycles",
                column: "DefaultCurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cycles_Currencies_AlternateCurrencyId",
                table: "Cycles");

            migrationBuilder.DropForeignKey(
                name: "FK_Cycles_Currencies_DefaultCurrencyId",
                table: "Cycles");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Cycles_AlternateCurrencyId",
                table: "Cycles");

            migrationBuilder.DropIndex(
                name: "IX_Cycles_DefaultCurrencyId",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "AlternateCurrencyId",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "DefaultCurrencyId",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Cycles");
        }
    }
}
