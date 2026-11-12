using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportBooking.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourtFormat",
                table: "Courts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndoor",
                table: "Courts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Surface",
                table: "Courts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourtFormat",
                table: "Courts");

            migrationBuilder.DropColumn(
                name: "IsIndoor",
                table: "Courts");

            migrationBuilder.DropColumn(
                name: "Surface",
                table: "Courts");
        }
    }
}
