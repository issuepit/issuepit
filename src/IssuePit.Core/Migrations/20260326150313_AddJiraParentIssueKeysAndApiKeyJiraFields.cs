using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssuePit.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraParentIssueKeysAndApiKeyJiraFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JiraBaseUrl",
                table: "jira_sync_configs");

            migrationBuilder.DropColumn(
                name: "JiraEmail",
                table: "jira_sync_configs");

            migrationBuilder.DropColumn(
                name: "OnlyImportWithParent",
                table: "jira_sync_configs");

            migrationBuilder.AddColumn<string>(
                name: "ParentIssueKeys",
                table: "jira_sync_configs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraBaseUrl",
                table: "api_keys",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraEmail",
                table: "api_keys",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentIssueKeys",
                table: "jira_sync_configs");

            migrationBuilder.DropColumn(
                name: "JiraBaseUrl",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "JiraEmail",
                table: "api_keys");

            migrationBuilder.AddColumn<string>(
                name: "JiraBaseUrl",
                table: "jira_sync_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraEmail",
                table: "jira_sync_configs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnlyImportWithParent",
                table: "jira_sync_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
