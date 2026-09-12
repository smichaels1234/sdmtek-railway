using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterIssuesAndUnsubscribeToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UnsubscribeToken",
                schema: "sdmtek_website",
                table: "NewsletterSubscribers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill existing rows with a unique token (avoids a unique-index violation from the shared default above).
            migrationBuilder.Sql(
                "UPDATE sdmtek_website.\"NewsletterSubscribers\" SET \"UnsubscribeToken\" = md5(random()::text || clock_timestamp()::text)::uuid;");

            migrationBuilder.CreateTable(
                name: "NewsletterIssues",
                schema: "sdmtek_website",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterIssues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeToken",
                schema: "sdmtek_website",
                table: "NewsletterSubscribers",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterIssues",
                schema: "sdmtek_website");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeToken",
                schema: "sdmtek_website",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribeToken",
                schema: "sdmtek_website",
                table: "NewsletterSubscribers");
        }
    }
}
