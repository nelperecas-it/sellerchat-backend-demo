using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCIABackendDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddCallDetailsToCallHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "CallHistories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "From",
                table: "CallHistories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "To",
                table: "CallHistories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "CallHistories");

            migrationBuilder.DropColumn(
                name: "From",
                table: "CallHistories");

            migrationBuilder.DropColumn(
                name: "To",
                table: "CallHistories");
        }
    }
}
