using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCausalityAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Importance",
                table: "simulation_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ImportanceScore",
                table: "simulation_events",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SelectedFactorsJson",
                table: "agent_decisions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_causes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CauseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CauseEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedTick = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_causes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_consequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConsequenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConsequenceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsequenceMemoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedTick = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_consequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "world_history_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    SimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Importance = table.Column<int>(type: "integer", nullable: false),
                    FactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RelatedEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_history_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_simulation_events_WorldId_Importance",
                table: "simulation_events",
                columns: new[] { "WorldId", "Importance" });

            migrationBuilder.CreateIndex(
                name: "IX_event_causes_CauseEventId",
                table: "event_causes",
                column: "CauseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_causes_DecisionRecordId",
                table: "event_causes",
                column: "DecisionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_event_causes_EventId",
                table: "event_causes",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_consequences_ConsequenceEventId",
                table: "event_consequences",
                column: "ConsequenceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_consequences_EventId",
                table: "event_consequences",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_world_history_entries_WorldId_Importance",
                table: "world_history_entries",
                columns: new[] { "WorldId", "Importance" });

            migrationBuilder.CreateIndex(
                name: "IX_world_history_entries_WorldId_SimulationTime",
                table: "world_history_entries",
                columns: new[] { "WorldId", "SimulationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_causes");

            migrationBuilder.DropTable(
                name: "event_consequences");

            migrationBuilder.DropTable(
                name: "world_history_entries");

            migrationBuilder.DropIndex(
                name: "IX_simulation_events_WorldId_Importance",
                table: "simulation_events");

            migrationBuilder.DropColumn(
                name: "Importance",
                table: "simulation_events");

            migrationBuilder.DropColumn(
                name: "ImportanceScore",
                table: "simulation_events");

            migrationBuilder.DropColumn(
                name: "SelectedFactorsJson",
                table: "agent_decisions");
        }
    }
}
