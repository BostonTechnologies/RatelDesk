using Helpdesk.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Helpdesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class update_KnowledgeBaseArticleId_nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeEmbeddings_KnowledgeBaseArticles_KnowledgeBaseArti~",
                table: "KnowledgeEmbeddings");

            migrationBuilder.AlterColumn<Guid>(
                name: "KnowledgeBaseArticleId",
                table: "KnowledgeEmbeddings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeEmbeddings_KnowledgeBaseArticles_KnowledgeBaseArti~",
                table: "KnowledgeEmbeddings",
                column: "KnowledgeBaseArticleId",
                principalTable: "KnowledgeBaseArticles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeEmbeddings_KnowledgeBaseArticles_KnowledgeBaseArti~",
                table: "KnowledgeEmbeddings");

            migrationBuilder.AlterColumn<Guid>(
                name: "KnowledgeBaseArticleId",
                table: "KnowledgeEmbeddings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeEmbeddings_KnowledgeBaseArticles_KnowledgeBaseArti~",
                table: "KnowledgeEmbeddings",
                column: "KnowledgeBaseArticleId",
                principalTable: "KnowledgeBaseArticles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
