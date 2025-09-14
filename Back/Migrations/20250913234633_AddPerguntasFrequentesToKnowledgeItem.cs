using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseConhecimento.Migrations
{
    /// <inheritdoc />
    public partial class AddPerguntasFrequentesToKnowledgeItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PerguntasFrequentes",
                table: "KnowledgeBase",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerguntasFrequentes",
                table: "KnowledgeBase");
        }
    }
}
