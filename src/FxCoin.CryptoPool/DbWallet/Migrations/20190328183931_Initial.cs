namespace FxCoin.CryptoPool.DbWallet.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Migrations;

    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HdAccount",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Seed = table.Column<string>(nullable: true),
                    ExtPubKey = table.Column<string>(nullable: true),
                    ChainCode = table.Column<byte[]>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HdAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HdAddress",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AccountId = table.Column<int>(nullable: false),
                    Address = table.Column<string>(maxLength: 64, nullable: true),
                    ScriptPubKey = table.Column<byte[]>(maxLength: 256, nullable: true),
                    PubKey = table.Column<byte[]>(maxLength: 128, nullable: true),
                    IsChange = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HdAddress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HdAddress_HdAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "HdAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TxRef",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AddressId = table.Column<int>(nullable: false),
                    TxId = table.Column<string>(maxLength: 64, nullable: true),
                    Index = table.Column<int>(nullable: false),
                    Amount = table.Column<long>(nullable: false),
                    InputBlock = table.Column<int>(nullable: true),
                    OutputBlock = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxRef", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TxRef_HdAddress_AddressId",
                        column: x => x.AddressId,
                        principalTable: "HdAddress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HdAddress_AccountId",
                table: "HdAddress",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TxRef_AddressId",
                table: "TxRef",
                column: "AddressId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TxRef");

            migrationBuilder.DropTable(
                name: "HdAddress");

            migrationBuilder.DropTable(
                name: "HdAccount");
        }
    }
}
