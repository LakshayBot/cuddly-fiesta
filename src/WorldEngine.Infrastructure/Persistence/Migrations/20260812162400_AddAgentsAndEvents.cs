using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BirthSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Alive = table.Column<bool>(type: "boolean", nullable: false),
                    DeathSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeathCause = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Money = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Hunger = table.Column<double>(type: "double precision", nullable: false),
                    Energy = table.Column<double>(type: "double precision", nullable: false),
                    Health = table.Column<double>(type: "double precision", nullable: false),
                    Happiness = table.Column<double>(type: "double precision", nullable: false),
                    Safety = table.Column<double>(type: "double precision", nullable: false),
                    SocialNeed = table.Column<double>(type: "double precision", nullable: false),
                    Curiosity = table.Column<double>(type: "double precision", nullable: false),
                    Aggression = table.Column<double>(type: "double precision", nullable: false),
                    Empathy = table.Column<double>(type: "double precision", nullable: false),
                    Sociability = table.Column<double>(type: "double precision", nullable: false),
                    Ambition = table.Column<double>(type: "double precision", nullable: false),
                    RiskTolerance = table.Column<double>(type: "double precision", nullable: false),
                    Discipline = table.Column<double>(type: "double precision", nullable: false),
                    Generosity = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "simulation_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    SimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulation_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_WorldId",
                table: "agents",
                column: "WorldId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_WorldId_Alive",
                table: "agents",
                columns: new[] { "WorldId", "Alive" });

            migrationBuilder.CreateIndex(
                name: "IX_simulation_events_WorldId_EventType",
                table: "simulation_events",
                columns: new[] { "WorldId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_simulation_events_WorldId_SimulationTime",
                table: "simulation_events",
                columns: new[] { "WorldId", "SimulationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "simulation_events");
        }
    }
}
