using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MpesaPaymentApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientFreelancerId",
                table: "MpesaTransactions",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FreelancerPayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MpesaTransactionId = table.Column<int>(type: "int", nullable: false),
                    FreelancerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConversationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatorConversationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MpesaReceiptNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreelancerPayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TillNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaybillNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaybillAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankAccountName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankBranchCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTransactions_RecipientFreelancerId",
                table: "MpesaTransactions",
                column: "RecipientFreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerPayouts_FreelancerId",
                table: "FreelancerPayouts",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerPayouts_MpesaTransactionId",
                table: "FreelancerPayouts",
                column: "MpesaTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerPayouts_Status",
                table: "FreelancerPayouts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutDetails_UserId",
                table: "PayoutDetails",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreelancerPayouts");

            migrationBuilder.DropTable(
                name: "PayoutDetails");

            migrationBuilder.DropIndex(
                name: "IX_MpesaTransactions_RecipientFreelancerId",
                table: "MpesaTransactions");

            migrationBuilder.DropColumn(
                name: "RecipientFreelancerId",
                table: "MpesaTransactions");
        }
    }
}
