using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class RenameBlockColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OutputBlock",
                table: "TxRef",
                newName: "ArrivalBlock");

            migrationBuilder.RenameColumn(
                name: "InputBlock",
                table: "TxRef",
                newName: "SpendingBlock");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SpendingBlock",
                table: "TxRef",
                newName: "InputBlock");

            migrationBuilder.RenameColumn(
                name: "ArrivalBlock",
                table: "TxRef",
                newName: "OutputBlock");
        }
    }
}
