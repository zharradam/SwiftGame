using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftGame.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsModerator",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsModerator",
                table: "Players");
        }
    }
}
