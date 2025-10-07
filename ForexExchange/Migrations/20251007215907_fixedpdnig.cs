using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class fixedpdnig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: Removed all DeleteData operations that were causing foreign key constraint failures
            // The seeded data should remain intact to maintain referential integrity
            // TaskItems table was already created in previous migration - no operations needed
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: No operations needed as no changes were made in Up method
            // TaskItems table creation was handled in previous migration
        }
    }
}
