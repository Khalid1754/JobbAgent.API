using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobbAgent.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "Runs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewJson",
                table: "Runs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRunId",
                table: "Runs",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "InterviewJson",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "SourceRunId",
                table: "Runs");
        }
    }
}
