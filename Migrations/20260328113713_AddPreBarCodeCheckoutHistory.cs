using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordsMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddPreBarCodeCheckoutHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "RecordItems",
                type: "TEXT",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "BarCode",
                table: "RecordItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecordItemId",
                table: "CheckoutHistory",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "PreBarCodeRecordId",
                table: "CheckoutHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreBarCodeRecords",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CIS = table.Column<string>(type: "TEXT", nullable: false),
                    RecordType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    BoxNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Digitized = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DestroyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CheckedOut = table.Column<bool>(type: "INTEGER", nullable: false),
                    Requested = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReadyForPickup = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckedOutToId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreBarCodeRecords", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PreBarCodeRecords_AspNetUsers_CheckedOutToId",
                        column: x => x.CheckedOutToId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutHistory_PreBarCodeRecordId",
                table: "CheckoutHistory",
                column: "PreBarCodeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PreBarCodeRecords_CheckedOutToId",
                table: "PreBarCodeRecords",
                column: "CheckedOutToId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutHistory_PreBarCodeRecords_PreBarCodeRecordId",
                table: "CheckoutHistory",
                column: "PreBarCodeRecordId",
                principalTable: "PreBarCodeRecords",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutHistory_PreBarCodeRecords_PreBarCodeRecordId",
                table: "CheckoutHistory");

            migrationBuilder.DropTable(
                name: "PreBarCodeRecords");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutHistory_PreBarCodeRecordId",
                table: "CheckoutHistory");

            migrationBuilder.DropColumn(
                name: "PreBarCodeRecordId",
                table: "CheckoutHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "RecordItems",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BarCode",
                table: "RecordItems",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecordItemId",
                table: "CheckoutHistory",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
