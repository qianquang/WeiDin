using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeiDin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationAndMessageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "Groups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "Friendships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

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
                name: "IX_Groups_ConversationId",
                table: "Groups",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_ConversationId",
                table: "Friendships",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RelationId",
                table: "Conversations",
                column: "RelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RelationType",
                table: "Conversations",
                column: "RelationType");

            migrationBuilder.CreateIndex(
                name: "IX_MessageConversationIndex_ConversationId",
                table: "MessageConversationIndex",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "MessageConversationIndex");

            migrationBuilder.DropIndex(
                name: "IX_Groups_ConversationId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_ConversationId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "Friendships");
        }
    }
}
