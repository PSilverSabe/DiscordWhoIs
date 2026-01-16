using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations;

/// <inheritdoc />
public partial class UpdateAddMultiAuthorFics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

        migrationBuilder.DropPrimaryKey(
            name: "PK_Fanfics",
            table: "Fanfics");

        migrationBuilder.DropColumn(
            name: "Id",
            table: "Fanfics");

        migrationBuilder.DropColumn(
            name: "Author",
            table: "Fanfics");

        migrationBuilder.DropColumn(
            name: "Real",
            table: "Alias");

        migrationBuilder.RenameColumn(
            name: "Alias",
            table: "Alias",
            newName: "AliasName");

        migrationBuilder.AddColumn<int>(
            name: "FanficId",
            table: "Fanfics",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0)
            .Annotation("Sqlite:Autoincrement", true);

        migrationBuilder.AddColumn<int>(
            name: "AuthorId",
            table: "Alias",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddPrimaryKey(
            name: "PK_Fanfics",
            table: "Fanfics",
            column: "FanficId");

        migrationBuilder.CreateTable(
            name: "Authors",
            columns: table => new
            {
                AuthorId = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Ao3ProfileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                FanficNetId = table.Column<int>(type: "INTEGER", nullable: true),
                FanficNetProfileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                DiscordId = table.Column<int>(type: "INTEGER", nullable: true),
                DiscordUsername = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastActiveAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Authors", x => x.AuthorId);
            });

        migrationBuilder.CreateTable(
            name: "AuthorFanfic",
            columns: table => new
            {
                AuthorsAuthorId = table.Column<int>(type: "INTEGER", nullable: false),
                FanficsFanficId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthorFanfic", x => new { x.AuthorsAuthorId, x.FanficsFanficId });
                table.ForeignKey(
                    name: "FK_AuthorFanfic_Authors_AuthorsAuthorId",
                    column: x => x.AuthorsAuthorId,
                    principalTable: "Authors",
                    principalColumn: "AuthorId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AuthorFanfic_Fanfics_FanficsFanficId",
                    column: x => x.FanficsFanficId,
                    principalTable: "Fanfics",
                    principalColumn: "FanficId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Alias_AuthorId",
            table: "Alias",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthorFanfic_FanficsFanficId",
            table: "AuthorFanfic",
            column: "FanficsFanficId");

        migrationBuilder.CreateIndex(
            name: "IX_Authors_Ao3ProfileName",
            table: "Authors",
            column: "Ao3ProfileName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Authors_DiscordId",
            table: "Authors",
            column: "DiscordId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Authors_DiscordUsername",
            table: "Authors",
            column: "DiscordUsername",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Authors_FanficNetId",
            table: "Authors",
            column: "FanficNetId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Authors_FanficNetProfileName",
            table: "Authors",
            column: "FanficNetProfileName",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Alias_Authors_AuthorId",
            table: "Alias",
            column: "AuthorId",
            principalTable: "Authors",
            principalColumn: "AuthorId",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

        migrationBuilder.DropForeignKey(
            name: "FK_Alias_Authors_AuthorId",
            table: "Alias");

        migrationBuilder.DropTable(
            name: "AuthorFanfic");

        migrationBuilder.DropTable(
            name: "Authors");

        migrationBuilder.DropPrimaryKey(
            name: "PK_Fanfics",
            table: "Fanfics");

        migrationBuilder.DropIndex(
            name: "IX_Alias_AuthorId",
            table: "Alias");

        migrationBuilder.DropColumn(
            name: "FanficId",
            table: "Fanfics");

        migrationBuilder.DropColumn(
            name: "AuthorId",
            table: "Alias");

        migrationBuilder.RenameColumn(
            name: "AliasName",
            table: "Alias",
            newName: "Alias");

        migrationBuilder.AddColumn<int>(
            name: "Id",
            table: "Fanfics",
            type: "INTEGER",
            maxLength: 64,
            nullable: false,
            defaultValue: 0)
            .Annotation("Sqlite:Autoincrement", true);

        migrationBuilder.AddColumn<string>(
            name: "Author",
            table: "Fanfics",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Real",
            table: "Alias",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Fanfics",
            table: "Fanfics",
            column: "Id");
    }
}
