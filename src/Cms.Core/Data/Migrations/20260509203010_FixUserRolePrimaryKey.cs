using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cms.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRolePrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sys_UserRoles_Sys_Roles_RoleId",
                table: "Sys_UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Sys_UserRoles_Sys_Users_UserId",
                table: "Sys_UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sys_UserRoles",
                table: "Sys_UserRoles");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Sys_UserRoles",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Sys_UserRoles",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sys_UserRoles",
                table: "Sys_UserRoles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Sys_UserRoles_UserId_RoleId_TenantId",
                table: "Sys_UserRoles",
                columns: new[] { "UserId", "RoleId", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sys_UserRoles_Sys_Roles_RoleId",
                table: "Sys_UserRoles",
                column: "RoleId",
                principalTable: "Sys_Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sys_UserRoles_Sys_Users_UserId",
                table: "Sys_UserRoles",
                column: "UserId",
                principalTable: "Sys_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sys_UserRoles_Sys_Roles_RoleId",
                table: "Sys_UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Sys_UserRoles_Sys_Users_UserId",
                table: "Sys_UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sys_UserRoles",
                table: "Sys_UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_Sys_UserRoles_UserId_RoleId_TenantId",
                table: "Sys_UserRoles");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Sys_UserRoles");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Sys_UserRoles",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sys_UserRoles",
                table: "Sys_UserRoles",
                columns: new[] { "UserId", "RoleId", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sys_UserRoles_Sys_Roles_RoleId",
                table: "Sys_UserRoles",
                column: "RoleId",
                principalTable: "Sys_Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sys_UserRoles_Sys_Users_UserId",
                table: "Sys_UserRoles",
                column: "UserId",
                principalTable: "Sys_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
