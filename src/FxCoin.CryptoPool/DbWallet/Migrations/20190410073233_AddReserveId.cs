using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class AddReserveId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservedBy",
                table: "TxRef",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedOn",
                table: "TxRef",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedBy",
                table: "TxRef");

            migrationBuilder.DropColumn(
                name: "ReservedOn",
                table: "TxRef");
        }
    }
}
