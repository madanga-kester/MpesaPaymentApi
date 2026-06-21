using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpesaPaymentApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginClientIdToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginClientId",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_OriginClientId",
                table: "MpesaTransactions",
                column: "OriginClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_OriginClientId",
                table: "MpesaTransactions");

            migrationBuilder.DropColumn(
                name: "OriginClientId",
                table: "MpesaTransactions");
        }
    }
}
