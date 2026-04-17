using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftGame.Data.SqlServer.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Players",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Players", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Songs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Album = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AlbumArt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ArtistName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PreviewUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                DurationMs = table.Column<int>(type: "int", nullable: false),
                ReleaseYear = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Songs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Scores",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SongId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PointsEarned = table.Column<int>(type: "int", nullable: false),
                ResponseTimeMs = table.Column<int>(type: "int", nullable: false),
                IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                PlayedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Scores", x => x.Id);
                table.ForeignKey(
                    name: "FK_Scores_Players_PlayerId",
                    column: x => x.PlayerId,
                    principalTable: "Players",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Scores_Songs_SongId",
                    column: x => x.SongId,
                    principalTable: "Songs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Players_Email",
            table: "Players",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Players_Username",
            table: "Players",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Scores_PlayerId",
            table: "Scores",
            column: "PlayerId");

        migrationBuilder.CreateIndex(
            name: "IX_Scores_SongId",
            table: "Scores",
            column: "SongId");

        migrationBuilder.CreateIndex(
            name: "IX_Songs_ProviderId_Provider",
            table: "Songs",
            columns: new[] { "ProviderId", "Provider" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Scores");

        migrationBuilder.DropTable(
            name: "Players");

        migrationBuilder.DropTable(
            name: "Songs");
    }
}
