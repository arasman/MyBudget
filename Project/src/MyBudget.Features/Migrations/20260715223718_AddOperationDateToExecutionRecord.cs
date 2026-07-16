using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBudget.Features.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationDateToExecutionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "OperationDate",
                table: "ExecutionRecords",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperationDate",
                table: "ExecutionRecords");
        }
    }
}
