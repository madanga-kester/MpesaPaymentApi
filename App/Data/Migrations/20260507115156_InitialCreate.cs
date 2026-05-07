using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpesaPaymentApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MpesaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckoutRequestID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MerchantRequestID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultCode = table.Column<int>(type: "int", nullable: true),
                    ResultDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MpesaReceiptNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CallbackReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MpesaTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_CheckoutRequestID",
                table: "MpesaTransactions",
                column: "CheckoutRequestID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MpesaTransactions");
        }
    }
}
