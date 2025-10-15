using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scoreboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCurrentQuarterFromMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            IF EXISTS(SELECT 1 FROM sys.columns
                    WHERE Name = N'CurrentQuarter' AND Object_ID = Object_ID(N'dbo.Matches'))
            BEGIN
                ALTER TABLE [dbo].[Matches] DROP COLUMN [CurrentQuarter];
            END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentQuarter",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
