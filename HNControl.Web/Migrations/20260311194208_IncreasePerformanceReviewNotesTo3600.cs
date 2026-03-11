using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNControl.Web.Migrations
{
    /// <inheritdoc />
    public partial class IncreasePerformanceReviewNotesTo3600 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PerformanceReviews",
                type: "character varying(3600)",
                maxLength: 3600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1200)",
                oldMaxLength: 1200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "PerformanceReviews",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3600)",
                oldMaxLength: 3600);
        }
    }
}
