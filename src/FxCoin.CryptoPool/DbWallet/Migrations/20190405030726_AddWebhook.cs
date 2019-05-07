using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    public partial class AddWebhook : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TxWebhook",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    TxRefId = table.Column<int>(nullable: false),
                    Created = table.Column<DateTime>(nullable: false),
                    SendOn = table.Column<DateTime>(nullable: true),
                    Status = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxWebhook", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TxWebhook_TxRef_TxRefId",
                        column: x => x.TxRefId,
                        principalTable: "TxRef",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TxWebhook_TxRefId",
                table: "TxWebhook",
                column: "TxRefId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TxWebhook");
        }
    }
}
