using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ResolveConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmbedPosterConfiguration",
                columns: new[] { "Id", "ChannelId", "DeduplicationWindowMinutes", "Enabled" },
                values: new object[] { 1, null, 10, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmbedPosterConfiguration",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
