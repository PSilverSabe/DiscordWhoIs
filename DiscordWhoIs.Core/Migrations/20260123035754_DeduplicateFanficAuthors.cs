using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateFanficAuthors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            // Remove duplicate FanficId + AuthorId pairs
            migrationBuilder.Sql(@"
                DELETE FROM AuthorFanfic
                WHERE rowid NOT IN (
                    SELECT MIN(rowid)
                    FROM AuthorFanfic
                    GROUP BY FanficId, AuthorId
                );
                ");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
