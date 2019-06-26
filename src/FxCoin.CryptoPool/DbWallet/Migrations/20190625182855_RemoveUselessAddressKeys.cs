using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class RemoveUselessAddressKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PubKey",
                table: "HdAddress");

            migrationBuilder.DropColumn(
                name: "ScriptPubKey",
                table: "HdAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PubKey",
                table: "HdAddress",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ScriptPubKey",
                table: "HdAddress",
                maxLength: 256,
                nullable: true);
        }
    }
}
