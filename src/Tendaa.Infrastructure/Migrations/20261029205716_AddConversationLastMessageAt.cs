using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tendaa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationLastMessageAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMessageAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "Conversations");
        }
    }
}
