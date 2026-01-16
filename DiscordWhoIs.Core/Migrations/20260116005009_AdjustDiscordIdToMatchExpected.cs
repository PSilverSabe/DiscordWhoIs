using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordWhoIs.Core.Migrations;

/// <inheritdoc />
public partial class AdjustDiscordIdToMatchExpected : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Alias",
            table: "Alias");

        migrationBuilder.AlterColumn<string>(
            name: "Ao3ProfileName",
            table: "Authors",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            collation: "NOCASE",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Authors",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "AliasName",
            table: "Alias",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            collation: "NOCASE",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256);

        migrationBuilder.AddColumn<int>(
            name: "Id",
            table: "Alias",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0)
            .Annotation("Sqlite:Autoincrement", true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_Alias",
            table: "Alias",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_Alias_AliasName",
            table: "Alias",
            column: "AliasName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Alias",
            table: "Alias");

        migrationBuilder.DropIndex(
            name: "IX_Alias_AliasName",
            table: "Alias");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "Authors");

        migrationBuilder.DropColumn(
            name: "Id",
            table: "Alias");

        migrationBuilder.AlterColumn<string>(
            name: "Ao3ProfileName",
            table: "Authors",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 256,
            oldCollation: "NOCASE");

        migrationBuilder.AlterColumn<string>(
            name: "AliasName",
            table: "Alias",
            type: "TEXT",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 200,
            oldCollation: "NOCASE");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Alias",
            table: "Alias",
            column: "AliasName");
    }
}
