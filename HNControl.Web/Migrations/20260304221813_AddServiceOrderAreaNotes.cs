using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOrderAreaNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LevantamientoNotes",
                table: "ServiceOrders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaterialesNotes",
                table: "ServiceOrders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevantamientoNotes",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "MaterialesNotes",
                table: "ServiceOrders");
        }
    }
}
