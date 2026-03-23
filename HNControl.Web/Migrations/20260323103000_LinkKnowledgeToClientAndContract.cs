using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    public partial class LinkKnowledgeToClientAndContract : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "KnowledgeLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientServiceContractId",
                table: "KnowledgeLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ClientId",
                table: "KnowledgeLinks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeLinks_ClientServiceContractId",
                table: "KnowledgeLinks",
                column: "ClientServiceContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContractId",
                table: "KnowledgeLinks",
                column: "ClientServiceContractId",
                principalTable: "ClientServiceContracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeLinks_Clients_ClientId",
                table: "KnowledgeLinks",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeLinks_ClientServiceContracts_ClientServiceContractId",
                table: "KnowledgeLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeLinks_Clients_ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeLinks_ClientServiceContractId",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "KnowledgeLinks");

            migrationBuilder.DropColumn(
                name: "ClientServiceContractId",
                table: "KnowledgeLinks");
        }
    }
}
