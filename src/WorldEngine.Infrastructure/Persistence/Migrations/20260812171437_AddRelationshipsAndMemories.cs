using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationshipsAndMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Importance = table.Column<double>(type: "double precision", nullable: false),
                    EmotionalImpact = table.Column<double>(type: "double precision", nullable: false),
                    CreatedSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OtherAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_memories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_relationships",
                columns: table => new
                {
                    SourceAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trust = table.Column<double>(type: "double precision", nullable: false),
                    Affection = table.Column<double>(type: "double precision", nullable: false),
                    Respect = table.Column<double>(type: "double precision", nullable: false),
                    Fear = table.Column<double>(type: "double precision", nullable: false),
                    Anger = table.Column<double>(type: "double precision", nullable: false),
                    Familiarity = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_relationships", x => new { x.SourceAgentId, x.TargetAgentId });
                    table.ForeignKey(
                        name: "FK_agent_relationships_agents_SourceAgentId",
                        column: x => x.SourceAgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_memories_AgentId_CreatedSimulationTime",
                table: "agent_memories",
                columns: new[] { "AgentId", "CreatedSimulationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_memories_SimulationEventId",
                table: "agent_memories",
                column: "SimulationEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_memories");

            migrationBuilder.DropTable(
                name: "agent_relationships");
        }
    }
}
