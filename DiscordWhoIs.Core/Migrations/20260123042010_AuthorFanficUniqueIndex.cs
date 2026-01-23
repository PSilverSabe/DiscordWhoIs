using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations
{
    /// <inheritdoc />
    public partial class AuthorFanficUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthorFanfic_FanficsFanficId",
                table: "AuthorFanfic");

            migrationBuilder.CreateIndex(
                name: "IX_FanficAuthors_FanficId_AuthorId",
                table: "AuthorFanfic",
                columns: new[] { "FanficsFanficId", "AuthorsAuthorId" },
                unique: true);

            // Remove duplicate FanficId + AuthorId pairs
            migrationBuilder.Sql(@"
                DELETE FROM AuthorFanfic
                WHERE rowid NOT IN (
                    SELECT MIN(rowid)
                    FROM AuthorFanfic
                    GROUP BY AuthorsAuthorId, FanficsFanficId
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FanficAuthors_FanficId_AuthorId",
                table: "AuthorFanfic");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorFanfic_FanficsFanficId",
                table: "AuthorFanfic",
                column: "FanficsFanficId");
        }
    }
}
