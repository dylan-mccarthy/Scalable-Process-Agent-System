using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlPlane.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "budget",
                table: "agents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "input_connector",
                table: "agents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "agents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "output_connector",
                table: "agents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tools",
                table: "agents",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "budget",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "description",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "input_connector",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "output_connector",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "tools",
                table: "agents");
        }
    }
}
