using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpesaPaymentApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeCheckoutRequestIDNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_CheckoutRequestID",
                table: "MpesaTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "CheckoutRequestID",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_CheckoutRequestID",
                table: "MpesaTransactions",
                column: "CheckoutRequestID",
                unique: true,
                filter: "[CheckoutRequestID] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_CheckoutRequestID",
                table: "MpesaTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "CheckoutRequestID",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_CheckoutRequestID",
                table: "MpesaTransactions",
                column: "CheckoutRequestID",
                unique: true);
        }
    }
}
