using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobbAgent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCvVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CvId",
                table: "Watches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CvId",
                table: "Runs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Profiles",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Mitt CV");

            // Det CV som fanns innan blir standard.
            migrationBuilder.Sql("UPDATE Profiles SET IsDefault = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvId",
                table: "Watches");

            migrationBuilder.DropColumn(
                name: "CvId",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Profiles");
        }
    }
}
