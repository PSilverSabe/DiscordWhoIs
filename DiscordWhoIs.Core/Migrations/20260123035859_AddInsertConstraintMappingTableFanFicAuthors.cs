using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddInsertConstraintMappingTableFanFicAuthors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
                name: "IX_FanficAuthors_FanficId_AuthorId",
                table: "FanficAuthors",
                columns: new[] { "FanficId", "AuthorId" },
                unique: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
