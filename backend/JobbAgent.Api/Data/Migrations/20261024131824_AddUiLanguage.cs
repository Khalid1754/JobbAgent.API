using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobbAgent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUiLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UiLanguage",
                table: "Watches",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "sv");

            migrationBuilder.AddColumn<string>(
                name: "UiLanguage",
                table: "Runs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "sv");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UiLanguage",
                table: "Watches");

            migrationBuilder.DropColumn(
                name: "UiLanguage",
                table: "Runs");
        }
    }
}
