using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEnterpriseInbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sender = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Urgency = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ActionPlan = table.Column<string>(type: "text", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TargetSystemStatus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_Category",
                table: "CustomerRequests",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_IsProcessed",
                table: "CustomerRequests",
                column: "IsProcessed");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_Urgency",
                table: "CustomerRequests",
                column: "Urgency");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerRequests");
        }
    }
}
