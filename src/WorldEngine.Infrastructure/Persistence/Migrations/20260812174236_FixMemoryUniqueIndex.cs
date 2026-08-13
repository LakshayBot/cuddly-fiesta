using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixMemoryUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_memories_SimulationEventId",
                table: "agent_memories");

            migrationBuilder.CreateIndex(
                name: "IX_agent_memories_SimulationEventId_AgentId",
                table: "agent_memories",
                columns: new[] { "SimulationEventId", "AgentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_memories_SimulationEventId_AgentId",
                table: "agent_memories");

            migrationBuilder.CreateIndex(
                name: "IX_agent_memories_SimulationEventId",
                table: "agent_memories",
                column: "SimulationEventId",
                unique: true);
        }
    }
}
