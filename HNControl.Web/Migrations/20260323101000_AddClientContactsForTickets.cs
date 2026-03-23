using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class AddClientContactsForTickets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContacts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_Email",
                table: "ClientContacts",
                columns: new[] { "ClientId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_IsPrimary",
                table: "ClientContacts",
                columns: new[] { "ClientId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_Name",
                table: "ClientContacts",
                columns: new[] { "ClientId", "Name" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientContacts");
        }
    }
}
