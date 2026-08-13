using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDecisionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    SimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SelectedActionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectedActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SelectedScore = table.Column<double>(type: "double precision", nullable: false),
                    AvailableActionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    FallbackUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_decisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_decisions_AgentId_DecidedAt",
                table: "agent_decisions",
                columns: new[] { "AgentId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_decisions_WorldId",
                table: "agent_decisions",
                column: "WorldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_decisions");
        }
    }
}
