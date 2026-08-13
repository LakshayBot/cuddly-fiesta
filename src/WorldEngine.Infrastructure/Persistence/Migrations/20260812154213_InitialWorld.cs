using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "worlds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RandomSeed = table.Column<int>(type: "integer", nullable: false),
                    CurrentSimulationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SimulationSpeed = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TickNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worlds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_worlds_Name",
                table: "worlds",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "worlds");
        }
    }
}
