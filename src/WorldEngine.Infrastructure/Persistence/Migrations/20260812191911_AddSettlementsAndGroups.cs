using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementsAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FormationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FormationSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CenterLocationName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Population = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FormationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FirstPopulationAtTick = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => new { x.GroupId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_group_memberships_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_WorldId",
                table: "groups",
                column: "WorldId");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_WorldId",
                table: "settlements",
                column: "WorldId");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_WorldId_CenterLocationName",
                table: "settlements",
                columns: new[] { "WorldId", "CenterLocationName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "settlements");

            migrationBuilder.DropTable(
                name: "groups");
        }
    }
}
