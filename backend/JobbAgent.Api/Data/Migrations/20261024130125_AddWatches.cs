using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobbAgent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WatchId",
                table: "Runs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Watches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemoteOnly = table.Column<bool>(type: "bit", nullable: false),
                    MinScore = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    SeenAdIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewMatches = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watches", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Watches");

            migrationBuilder.DropColumn(
                name: "WatchId",
                table: "Runs");
        }
    }
}
