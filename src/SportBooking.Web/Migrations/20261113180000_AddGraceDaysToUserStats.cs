using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportBooking.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGraceDaysToUserStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GraceDaysUsedThisMonth",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraceDaysUsedThisMonth",
                table: "UserStats");
        }
    }
}
