using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierServiceTypeAndNetworkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fqdn",
                table: "ClientCarrierServices",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "ClientCarrierServices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GatewayLink",
                table: "ClientCarrierServices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "ClientCarrierServices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fqdn",
                table: "ClientCarrierServices");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "ClientCarrierServices");

            migrationBuilder.DropColumn(
                name: "GatewayLink",
                table: "ClientCarrierServices");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "ClientCarrierServices");
        }
    }
}
