using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class AddIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Index",
                table: "HdAddress",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Index",
                table: "HdAccount",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_HdAccount_Index",
                table: "HdAccount",
                column: "Index");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_HdAccount_Index",
                table: "HdAccount");

            migrationBuilder.DropColumn(
                name: "Index",
                table: "HdAddress");

            migrationBuilder.DropColumn(
                name: "Index",
                table: "HdAccount");
        }
    }
}
