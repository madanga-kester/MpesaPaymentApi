using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpesaPaymentApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIndexesAndUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_CreatedAt",
                table: "MpesaTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_PhoneNumber",
                table: "MpesaTransactions",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_Status",
                table: "MpesaTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_UserId",
                table: "MpesaTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_UserId_Status_CreatedAt",
                table: "MpesaTransactions",
                columns: new[] { "UserId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_CreatedAt",
                table: "MpesaTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_PhoneNumber",
                table: "MpesaTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_Status",
                table: "MpesaTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_UserId",
                table: "MpesaTransactions");

            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_UserId_Status_CreatedAt",
                table: "MpesaTransactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MpesaTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MpesaTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "MpesaTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
