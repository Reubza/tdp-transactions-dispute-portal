using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeAppeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "appeal_of_dispute_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_one_appeal_per_dispute",
                table: "disputes",
                column: "appeal_of_dispute_id",
                unique: true,
                filter: "appeal_of_dispute_id IS NOT NULL AND status <> 'Withdrawn'");

            migrationBuilder.AddForeignKey(
                name: "fk_disputes_disputes_appeal_of_dispute_id",
                table: "disputes",
                column: "appeal_of_dispute_id",
                principalTable: "disputes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_disputes_disputes_appeal_of_dispute_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "ix_disputes_one_appeal_per_dispute",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "appeal_of_dispute_id",
                table: "disputes");
        }
    }
}
