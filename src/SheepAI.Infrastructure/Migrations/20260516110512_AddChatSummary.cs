using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SheepAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "chats",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summary",
                table: "chats");
        }
    }
}
