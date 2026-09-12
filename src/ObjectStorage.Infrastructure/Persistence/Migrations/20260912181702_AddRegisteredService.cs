using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObjectStorage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegisteredService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisteredServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AllowedPrefixesRaw = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredServices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredServices_ApiKeyHash",
                table: "RegisteredServices",
                column: "ApiKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredServices_Name",
                table: "RegisteredServices",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegisteredServices");
        }
    }
}
