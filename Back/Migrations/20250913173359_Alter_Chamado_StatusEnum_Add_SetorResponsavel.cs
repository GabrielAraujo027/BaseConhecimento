using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseConhecimento.Migrations
{
    /// <inheritdoc />
    public partial class Alter_Chamado_StatusEnum_Add_SetorResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "Chamados");

            migrationBuilder.RenameColumn(
                name: "titulo",
                table: "Chamados",
                newName: "Titulo");

            migrationBuilder.RenameColumn(
                name: "descricao",
                table: "Chamados",
                newName: "Descricao");

            migrationBuilder.AddColumn<string>(
                name: "SetorResponsavel",
                table: "Chamados",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StatusEnum",
                table: "Chamados",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SetorResponsavel",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "StatusEnum",
                table: "Chamados");

            migrationBuilder.RenameColumn(
                name: "Titulo",
                table: "Chamados",
                newName: "titulo");

            migrationBuilder.RenameColumn(
                name: "Descricao",
                table: "Chamados",
                newName: "descricao");

            migrationBuilder.AddColumn<bool>(
                name: "status",
                table: "Chamados",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
