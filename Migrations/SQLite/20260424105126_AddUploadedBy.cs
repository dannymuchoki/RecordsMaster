using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordsMaster.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class AddUploadedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadedBy",
                table: "RecordItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedBy",
                table: "PreBarCodeRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "RecordItems");

            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "PreBarCodeRecords");
        }
    }
}
