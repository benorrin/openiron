using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIron.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShareActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShareActivity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShareActivity",
                table: "Users");
        }
    }
}
