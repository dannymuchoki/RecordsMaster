using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordsMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutHistoryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutHistory_AspNetUsers_UserId",
                table: "CheckoutHistory");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutHistory_AspNetUsers_UserId",
                table: "CheckoutHistory",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutHistory_AspNetUsers_UserId",
                table: "CheckoutHistory");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutHistory_AspNetUsers_UserId",
                table: "CheckoutHistory",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
