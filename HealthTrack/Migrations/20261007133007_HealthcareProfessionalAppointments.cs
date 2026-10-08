using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class HealthcareProfessionalAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_TrainerId_AppointmentDate_AppointmentTime",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "TrainerId",
                table: "Appointments",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "HealthcareProfessionalId",
                table: "Appointments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HealthcareProfessionals",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthcareProfessionals", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_HealthcareProfessionals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_HealthcareProfessionalId_AppointmentDate_AppointmentTime",
                table: "Appointments",
                columns: new[] { "HealthcareProfessionalId", "AppointmentDate", "AppointmentTime" },
                unique: true,
                filter: "[Status] IN (1, 2) AND [HealthcareProfessionalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TrainerId_AppointmentDate_AppointmentTime",
                table: "Appointments",
                columns: new[] { "TrainerId", "AppointmentDate", "AppointmentTime" },
                unique: true,
                filter: "[Status] IN (1, 2) AND [TrainerId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointment_ExactlyOneProvider",
                table: "Appointments",
                sql: "([TrainerId] IS NOT NULL AND [HealthcareProfessionalId] IS NULL) OR ([TrainerId] IS NULL AND [HealthcareProfessionalId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_HealthcareProfessionals_HealthcareProfessionalId",
                table: "Appointments",
                column: "HealthcareProfessionalId",
                principalTable: "HealthcareProfessionals",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_HealthcareProfessionals_HealthcareProfessionalId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "HealthcareProfessionals");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_HealthcareProfessionalId_AppointmentDate_AppointmentTime",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TrainerId_AppointmentDate_AppointmentTime",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointment_ExactlyOneProvider",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "HealthcareProfessionalId",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "TrainerId",
                table: "Appointments",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TrainerId_AppointmentDate_AppointmentTime",
                table: "Appointments",
                columns: new[] { "TrainerId", "AppointmentDate", "AppointmentTime" },
                unique: true,
                filter: "[Status] IN (1, 2)");
        }
    }
}
