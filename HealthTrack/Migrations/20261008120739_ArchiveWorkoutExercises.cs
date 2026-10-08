using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveWorkoutExercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId",
                table: "WorkoutLogs");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "WorkoutExercises",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId",
                table: "WorkoutLogs",
                column: "WorkoutExerciseId",
                principalTable: "WorkoutExercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "WorkoutExercises");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutLogs_WorkoutExercises_WorkoutExerciseId",
                table: "WorkoutLogs",
                column: "WorkoutExerciseId",
                principalTable: "WorkoutExercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
