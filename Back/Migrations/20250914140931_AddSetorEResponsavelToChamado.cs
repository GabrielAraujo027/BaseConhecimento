using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseConhecimento.Migrations
{
    /// <inheritdoc />
    public partial class AddSetorEResponsavelToChamado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Data",
                table: "Chamados",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Solicitante",
                table: "Chamados",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Data",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "Solicitante",
                table: "Chamados");
        }
    }
}
