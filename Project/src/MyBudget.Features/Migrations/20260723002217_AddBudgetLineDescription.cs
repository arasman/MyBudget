using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetLineDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "BudgetLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "BudgetLines");
        }
    }
}
