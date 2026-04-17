using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftGame.Data.SqlServer.Migrations;

/// <inheritdoc />
public partial class AddGameSession : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Scores_Players_PlayerId",
            table: "Scores");

        migrationBuilder.AddColumn<Guid>(
            name: "GameSessionId",
            table: "Scores",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.CreateTable(
            name: "GameSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TotalPoints = table.Column<int>(type: "int", nullable: false),
                QuestionsCount = table.Column<int>(type: "int", nullable: false),
                IsComplete = table.Column<bool>(type: "bit", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_GameSessions_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Scores_GameSessionId",
            table: "Scores",
            column: "GameSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_GameSessions_PlayerId",
            table: "GameSessions",
            column: "PlayerId");

        migrationBuilder.AddForeignKey(
            name: "FK_Scores_GameSessions_GameSessionId",
            table: "Scores",
            column: "GameSessionId",
            principalTable: "GameSessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Scores_Players_PlayerId",
            table: "Scores",
            column: "PlayerId",
            principalTable: "Players",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Scores_GameSessions_GameSessionId",
            table: "Scores");

        migrationBuilder.DropForeignKey(
            name: "FK_Scores_Players_PlayerId",
            table: "Scores");

        migrationBuilder.DropTable(
            name: "GameSessions");

        migrationBuilder.DropIndex(
            name: "IX_Scores_GameSessionId",
            table: "Scores");

        migrationBuilder.DropColumn(
            name: "GameSessionId",
            table: "Scores");

        migrationBuilder.AddForeignKey(
            name: "FK_Scores_Players_PlayerId",
            table: "Scores",
            column: "PlayerId",
            principalTable: "Players",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
