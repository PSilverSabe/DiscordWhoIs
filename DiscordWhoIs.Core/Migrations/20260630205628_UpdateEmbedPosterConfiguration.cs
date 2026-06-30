using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmbedPosterConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmbedPosterConfiguration",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "DeduplicationWindowMinutes",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "ChannelId",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(ulong),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "EmbedPosterConfiguration",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "EmbedPosterConfiguration",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordServerId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmbedPosterConfiguration_ServerId_ChannelId",
                table: "EmbedPosterConfiguration",
                columns: new[] { "ServerId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servers_DiscordServerId",
                table: "Servers",
                column: "DiscordServerId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EmbedPosterConfiguration_Servers_ServerId",
                table: "EmbedPosterConfiguration",
                column: "ServerId",
                principalTable: "Servers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmbedPosterConfiguration_Servers_ServerId",
                table: "EmbedPosterConfiguration");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_EmbedPosterConfiguration_ServerId_ChannelId",
                table: "EmbedPosterConfiguration");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "EmbedPosterConfiguration");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "EmbedPosterConfiguration");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "EmbedPosterConfiguration");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "DeduplicationWindowMinutes",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 10);

            migrationBuilder.AlterColumn<ulong>(
                name: "ChannelId",
                table: "EmbedPosterConfiguration",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");
        }
    }
}
