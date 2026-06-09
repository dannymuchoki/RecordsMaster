using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecordsMaster.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class ExpungedBoolean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Expunged",
                table: "RecordItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Expunged",
                table: "RecordItems");
        }
    }
}
