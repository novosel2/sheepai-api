using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SheepAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatIsFinished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_finished",
                table: "chats",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_finished",
                table: "chats");
        }
    }
}
