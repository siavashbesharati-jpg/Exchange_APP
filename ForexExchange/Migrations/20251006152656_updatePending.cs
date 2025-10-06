using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class updatePending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "ExchangeRates",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayOrder", "IsActive", "Name", "PersianName", "RatePriority", "Symbol" },
                values: new object[,]
                {
                    { 1, "IRR", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 1, true, "Iranian Toman", "تومان", 0, "﷼" },
                    { 2, "USD", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 4, true, "US Dollar", "دلار آمریکا", 0, "$" },
                    { 3, "EUR", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 5, true, "Euro", "یورو", 0, "€" },
                    { 4, "AED", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 3, true, "UAE Dirham", "درهم امارات", 0, "د.إ" },
                    { 5, "OMR", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 2, true, "Omani Rial", "ریال عمان", 0, "ر.ع." },
                    { 6, "TRY", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 6, true, "Turkish Lira", "لیر ترکیه", 0, "₺" },
                    { 7, "CNY", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), 7, true, "Chinese Yuan", "یوان چین", 0, "¥" }
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "CreatedAt", "DataType", "Description", "IsActive", "SettingKey", "SettingValue", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "decimal", "نرخ کمیسیون به درصد", true, "COMMISSION_RATE", "0.5", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 2, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "decimal", "کارمزد تبدیل ارز به درصد", true, "EXCHANGE_FEE_RATE", "0.2", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 3, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "decimal", "حداقل مبلغ تراکنش به تومان", true, "MIN_TRANSACTION_AMOUNT", "10000", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 4, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "decimal", "حداکثر مبلغ تراکنش به تومان", true, "MAX_TRANSACTION_AMOUNT", "1000000000", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 5, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "decimal", "محدودیت تراکنش روزانه به تومان", true, "DAILY_TRANSACTION_LIMIT", "5000000000", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 6, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "bool", "حالت تعمیرات سیستم", true, "SYSTEM_MAINTENANCE", "false", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 7, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "string", "ارز پیش‌فرض سیستم", true, "DEFAULT_CURRENCY", "USD", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 8, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "int", "بازه بروزرسانی نرخ ارز به دقیقه", true, "RATE_UPDATE_INTERVAL", "60", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 9, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "bool", "فعال‌سازی سیستم اعلان‌ها", true, "NOTIFICATION_ENABLED", "true", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 10, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "bool", "فعال‌سازی پشتیبان‌گیری خودکار", true, "BACKUP_ENABLED", "true", new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" }
                });

            migrationBuilder.InsertData(
                table: "CurrencyPools",
                columns: new[] { "Id", "ActiveBuyOrderCount", "ActiveSellOrderCount", "Balance", "CurrencyCode", "CurrencyId", "IsActive", "LastUpdated", "Notes", "RiskLevel", "TotalBought", "TotalSold" },
                values: new object[,]
                {
                    { 1, 0, 0, 0m, "", 1, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "Iranian Toman pool - initial setup", 1, 0m, 0m },
                    { 2, 0, 0, 0m, "", 2, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "US Dollar pool - initial setup", 1, 0m, 0m },
                    { 3, 0, 0, 0m, "", 3, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "Euro pool - initial setup", 1, 0m, 0m },
                    { 4, 0, 0, 0m, "", 4, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "UAE Dirham pool - initial setup", 1, 0m, 0m },
                    { 5, 0, 0, 0m, "", 5, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "Omani Rial pool - initial setup", 1, 0m, 0m },
                    { 6, 0, 0, 0m, "", 6, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "Turkish Lira pool - initial setup", 1, 0m, 0m },
                    { 7, 0, 0, 0m, "", 7, true, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "Chinese Yuan pool - initial setup", 1, 0m, 0m }
                });

            migrationBuilder.InsertData(
                table: "ExchangeRates",
                columns: new[] { "Id", "AverageBuyRate", "AverageSellRate", "FromCurrencyId", "IsActive", "Rate", "ToCurrencyId", "TotalBuyVolume", "TotalSellVolume", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, null, 1, true, 68500m, 2, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 2, null, null, 1, true, 72500m, 3, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 3, null, null, 1, true, 18750m, 4, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 4, null, null, 1, true, 178000m, 5, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 5, null, null, 1, true, 2000m, 6, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 6, null, null, 2, true, 0.000014598540145985401459854m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 7, null, null, 3, true, 0.0000137931034482758620689655m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 8, null, null, 4, true, 0.0000533333333333333333333333m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 9, null, null, 5, true, 0.0000056179775280898876404494m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 10, null, null, 6, true, 0.0005m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 11, null, null, 2, true, 0.93m, 3, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 12, null, null, 2, true, 3.68m, 4, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 13, null, null, 2, true, 0.385m, 5, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 14, null, null, 2, true, 34.85m, 6, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 15, null, null, 1, true, 9600m, 7, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 16, null, null, 7, true, 0.0001041666666666666666666667m, 1, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { 17, null, null, 2, true, 7.14m, 7, 0m, 0m, new DateTime(2025, 8, 18, 12, 0, 0, 0, DateTimeKind.Utc), "System" }
                });
        }
    }
}
