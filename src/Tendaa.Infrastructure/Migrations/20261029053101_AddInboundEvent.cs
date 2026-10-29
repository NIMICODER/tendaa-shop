using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tendaa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Bsuid = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundEvents_WhatsAppMessageId",
                table: "InboundEvents",
                column: "WhatsAppMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundEvents");
        }
    }
}
