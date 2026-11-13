using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportBooking.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Khách hiện có bắt đầu với 100 điểm uy tín
            migrationBuilder.AddColumn<int>(
                name: "TrustScore",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.CreateTable(
                name: "Staffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CourtId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Staffs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Staffs_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    CourtId = table.Column<int>(type: "int", nullable: false),
                    ReporterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsRealTime = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlaDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerResponse = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Compensation = table.Column<int>(type: "int", nullable: false),
                    DuplicateOfId = table.Column<int>(type: "int", nullable: true),
                    ReportCount = table.Column<int>(type: "int", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_AspNetUsers_HandledById",
                        column: x => x.HandledById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Incidents_DuplicateOfId",
                        column: x => x.DuplicateOfId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentLogs_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_Staffs_CourtId", table: "Staffs", column: "CourtId");
            migrationBuilder.CreateIndex(name: "IX_Staffs_UserId_CourtId", table: "Staffs", columns: new[] { "UserId", "CourtId" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_Incidents_BookingId_IsRealTime", table: "Incidents", columns: new[] { "BookingId", "IsRealTime" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_Incidents_CourtId_Status", table: "Incidents", columns: new[] { "CourtId", "Status" });
            migrationBuilder.CreateIndex(name: "IX_Incidents_DuplicateOfId", table: "Incidents", column: "DuplicateOfId");
            migrationBuilder.CreateIndex(name: "IX_Incidents_HandledById", table: "Incidents", column: "HandledById");
            migrationBuilder.CreateIndex(name: "IX_Incidents_ReporterId", table: "Incidents", column: "ReporterId");
            migrationBuilder.CreateIndex(name: "IX_Incidents_Status_SlaDeadline", table: "Incidents", columns: new[] { "Status", "SlaDeadline" });

            migrationBuilder.CreateIndex(name: "IX_IncidentLogs_IncidentId", table: "IncidentLogs", column: "IncidentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IncidentLogs");
            migrationBuilder.DropTable(name: "Incidents");
            migrationBuilder.DropTable(name: "Staffs");
            migrationBuilder.DropColumn(name: "TrustScore", table: "AspNetUsers");
        }
    }
}
