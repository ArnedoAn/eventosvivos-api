using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EventosVivos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedDataAndDevUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"));

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Capacity", "Name" },
                values: new object[] { 60000, "Estadio El Campín" });

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Capacity", "City", "Name" },
                values: new object[] { 1400, "Bogotá", "Teatro Colón" });

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Capacity", "City", "Name" },
                values: new object[] { 9000, "Bogotá", "Movistar Arena" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), "admin@eventosvivos.com", "$2a$11$LQ5iQL/ZfsZJOQDnrH.6peU7CxCg7FpnYfp30wVdSF4.bxOKRM37i", "Admin" },
                    { new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"), "user@eventosvivos.com", "$2a$11$jV3jsyE.9ewiwreqVJDWOOqF5uHANGAJv14Oigj.N7Gn14LXMBG6i", "User" }
                });

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Capacity", "Name" },
                values: new object[] { 500, "Teatro Municipal" });

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Capacity", "City", "Name" },
                values: new object[] { 1000, "Medellín", "Centro de Convenciones" });

            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Capacity", "City", "Name" },
                values: new object[] { 300, "Cali", "Sala de Conciertos" });
        }
    }
}
