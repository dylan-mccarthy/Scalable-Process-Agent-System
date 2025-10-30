using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControlPlane.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    model_profile = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.agent_id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    node_id = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    capacity = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "jsonb", nullable: true),
                    heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.node_id);
                });

            migrationBuilder.CreateTable(
                name: "agent_versions",
                columns: table => new
                {
                    version_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    spec = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_versions", x => x.version_id);
                    table.ForeignKey(
                        name: "FK_agent_versions_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "agent_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployments",
                columns: table => new
                {
                    dep_id = table.Column<string>(type: "text", nullable: false),
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    env = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deployments", x => x.dep_id);
                    table.ForeignKey(
                        name: "FK_deployments_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "agent_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "text", nullable: false),
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    dep_id = table.Column<string>(type: "text", nullable: true),
                    node_id = table.Column<string>(type: "text", nullable: true),
                    input_ref = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    timings = table.Column<string>(type: "jsonb", nullable: true),
                    costs = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.run_id);
                    table.ForeignKey(
                        name: "FK_runs_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "agent_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_runs_deployments_dep_id",
                        column: x => x.dep_id,
                        principalTable: "deployments",
                        principalColumn: "dep_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_runs_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "node_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_versions_agent_id_version",
                table: "agent_versions",
                columns: new[] { "agent_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_name",
                table: "agents",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_deployments_agent_id_version_env",
                table: "deployments",
                columns: new[] { "agent_id", "version", "env" });

            migrationBuilder.CreateIndex(
                name: "IX_nodes_heartbeat_at",
                table: "nodes",
                column: "heartbeat_at");

            migrationBuilder.CreateIndex(
                name: "IX_runs_agent_id_status",
                table: "runs",
                columns: new[] { "agent_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_created_at",
                table: "runs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_runs_dep_id",
                table: "runs",
                column: "dep_id");

            migrationBuilder.CreateIndex(
                name: "IX_runs_node_id",
                table: "runs",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "IX_runs_status",
                table: "runs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_versions");

            migrationBuilder.DropTable(
                name: "runs");

            migrationBuilder.DropTable(
                name: "deployments");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
