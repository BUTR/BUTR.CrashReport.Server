using Microsoft.EntityFrameworkCore.Migrations;

using System;

#nullable disable

namespace BUTR.CrashReport.Server.Migrations
{
    public partial class NewMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "crash_report_id",
                table: "file_entity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("""
                                 UPDATE file_entity fe
                                 SET crash_report_id = (SELECT ie.crash_report_id FROM id_entity ie WHERE ie.file_id = fe.file_id)
                                 """); 
            migrationBuilder.DropForeignKey(
                name: "FK_file_entity_id_entity_file_id",
                table: "file_entity");
            migrationBuilder.Sql("""
                                 DELETE FROM    file_entity T1
                                    USING       file_entity T2
                                 WHERE  T1.ctid    < T2.ctid
                                    AND  T1.crash_report_id    = T2.crash_report_id;
                                 """);
            
            
            migrationBuilder.AddColumn<Guid>(
                name: "crash_report_id",
                table: "json_entity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("""
                                 UPDATE json_entity je
                                 SET crash_report_id = (SELECT ie.crash_report_id FROM id_entity ie WHERE ie.file_id = je.file_id)
                                 """);
            migrationBuilder.DropForeignKey(
                name: "FK_json_entity_id_entity_file_id",
                table: "json_entity");
            migrationBuilder.Sql("""
                                 DELETE   FROM json_entity T1
                                   USING       json_entity T2
                                 WHERE  T1.ctid    < T2.ctid
                                   AND  T1.crash_report_id    = T2.crash_report_id;
                                 """);


            migrationBuilder.DropPrimaryKey(
                name: "file_entity_pkey",
                table: "file_entity");
            migrationBuilder.DropColumn(
                name: "file_id",
                table: "file_entity");
            migrationBuilder.AddPrimaryKey(
                name: "html_entity_pkey",
                table: "file_entity",
                column: "crash_report_id");



            migrationBuilder.DropPrimaryKey(
                name: "PK_json_entity",
                table: "json_entity");
            migrationBuilder.DropColumn(
                name: "file_id",
                table: "json_entity");
            migrationBuilder.AddPrimaryKey(
                name: "json_entity_pkey",
                table: "json_entity",
                column: "crash_report_id");
            
            
            migrationBuilder.CreateTable(
                name: "id_test_entity",
                columns: table => new
                {
                    file_id = table.Column<string>(type: "text", nullable: false),
                    crash_report_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("id_entity_pkey", x => x.file_id);
                });
            migrationBuilder.CreateIndex(
                name: "id_entity_file_id_idx",
                table: "id_test_entity",
                column: "file_id");
            migrationBuilder.Sql("""
                                 INSERT INTO id_test_entity (crash_report_id, file_id)
                                 SELECT ie.crash_report_id, ie.file_id
                                 FROM id_entity ie
                                 """);


            migrationBuilder.Sql("""
                                 DELETE  FROM    id_entity T1
                                         USING   id_entity T2
                                 WHERE  T1.ctid              < T2.ctid
                                   AND  T1.crash_report_id   = T2.crash_report_id;
                                 """);
            migrationBuilder.DropPrimaryKey(
                name: "PK_id_entity",
                table: "id_entity");
            migrationBuilder.DropColumn(
                name: "file_id",
                table: "id_entity");
            migrationBuilder.DropIndex(
                name: "IX_id_entity_crash_report_id",
                table: "id_entity");
            migrationBuilder.AddPrimaryKey(
                name: "report_entity_pkey",
                table: "id_entity",
                column: "crash_report_id");
            migrationBuilder.AddColumn<byte>(
                name: "tenant",
                table: "id_entity",
                type: "smallint",
                nullable: false,
                defaultValue: (byte) 0);

            
            migrationBuilder.AddForeignKey(
                name: "report_entity_html_entity_fkey",
                table: "file_entity",
                column: "crash_report_id",
                principalTable: "id_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "report_entity_json_entity_fkey",
                table: "json_entity",
                column: "crash_report_id",
                principalTable: "id_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
            
            
            migrationBuilder.AddForeignKey(
                name: "html_entity_id_entity_fkey",
                table: "id_test_entity",
                column: "crash_report_id",
                principalTable: "file_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "report_entity_id_entity_fkey",
                table: "id_test_entity",
                column: "crash_report_id",
                principalTable: "id_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "json_entity_id_entity_fkey",
                table: "id_test_entity",
                column: "crash_report_id",
                principalTable: "id_entity",
                principalColumn: "crash_report_id",
                onDelete: ReferentialAction.Cascade);
            

            migrationBuilder.RenameTable(
                name: "id_entity",
                newName: "report_entity");
            migrationBuilder.RenameTable(
                name: "file_entity",
                newName: "html_entity");
            migrationBuilder.RenameTable(
                name: "id_test_entity",
                newName: "id_entity");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}