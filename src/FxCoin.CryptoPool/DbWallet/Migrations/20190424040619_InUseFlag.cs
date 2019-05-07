using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class InUseFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInUse",
                table: "HdAddress",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInUse",
                table: "HdAddress");
        }
    }
}
