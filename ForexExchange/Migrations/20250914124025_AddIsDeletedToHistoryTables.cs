using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CustomerBalanceHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "CustomerBalanceHistory",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CustomerBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CurrencyPoolHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "CurrencyPoolHistory",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CurrencyPoolHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "BankAccountBalanceHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "BankAccountBalanceHistory",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BankAccountBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BankAccountBalanceHistory");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "BankAccountBalanceHistory");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BankAccountBalanceHistory");
        }
    }
}
