using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftGame.Data.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddModesAndProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "Players",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FavouriteEra",
                table: "Players",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeOf",
                table: "GameSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "GameSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "classic");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_IsComplete_Mode_CompletedAt",
                table: "GameSessions",
                columns: new[] { "IsComplete", "Mode", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSessions_IsComplete_Mode_CompletedAt",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "FavouriteEra",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ChallengeOf",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "GameSessions");
        }
    }
}
