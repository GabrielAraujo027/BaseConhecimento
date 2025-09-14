using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseConhecimento.Migrations
{
    /// <inheritdoc />
    public partial class MakeDataConclusaoNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Data",
                table: "Chamados",
                newName: "DataCriacao");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataConclusao",
                table: "Chamados",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataConclusao",
                table: "Chamados");

            migrationBuilder.RenameColumn(
                name: "DataCriacao",
                table: "Chamados",
                newName: "Data");
        }
    }
}
