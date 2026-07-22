using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetLineXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column — no DDL needed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column — no DDL needed.
        }
    }
}
