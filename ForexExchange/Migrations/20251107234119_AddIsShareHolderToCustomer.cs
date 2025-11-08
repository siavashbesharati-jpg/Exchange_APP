using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddIsShareHolderToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShareHolder",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShareHolder",
                table: "Customers");
        }
    }
}
