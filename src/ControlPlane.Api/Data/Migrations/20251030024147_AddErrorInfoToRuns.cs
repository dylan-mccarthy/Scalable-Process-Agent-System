using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorInfoToRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_info",
                table: "runs",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_info",
                table: "runs");
        }
    }
}
