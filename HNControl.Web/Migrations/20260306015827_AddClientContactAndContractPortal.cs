using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddClientContactAndContractPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortalPasswordProtected",
                table: "ClientServiceContracts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PortalUrl",
                table: "ClientServiceContracts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PortalUsername",
                table: "ClientServiceContracts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortalPasswordProtected",
                table: "ClientServiceContracts");

            migrationBuilder.DropColumn(
                name: "PortalUrl",
                table: "ClientServiceContracts");

            migrationBuilder.DropColumn(
                name: "PortalUsername",
                table: "ClientServiceContracts");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Clients");
        }
    }
}
