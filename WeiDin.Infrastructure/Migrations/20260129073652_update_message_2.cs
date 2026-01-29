using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeiDin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class update_message_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageConversationIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageConversationIndex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageConversationIndex", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageConversationIndex_ConversationId",
                table: "MessageConversationIndex",
                column: "ConversationId");
        }
    }
}
