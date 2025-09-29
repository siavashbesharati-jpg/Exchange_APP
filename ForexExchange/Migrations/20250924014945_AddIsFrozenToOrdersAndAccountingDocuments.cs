using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFrozenToOrdersAndAccountingDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "BankAccountBalanceHistory");

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "AccountingDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "AccountingDocuments");

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "CustomerBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "CurrencyPoolHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "BankAccountBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
