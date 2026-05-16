using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SheepAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileCategoryColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "files",
                type: "text",
                nullable: false,
                defaultValue: "General");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                table: "files");
        }
    }
}
