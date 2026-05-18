using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalaFinder.Models;

namespace SalaFinder.Data
{
    // Demo seed for SalaFinder. Idempotent: each section checks counts/existence
    // before inserting, so the seeder is safe to run on every startup.
    //
    // Project spec thresholds covered here:
    //   - >=15 users (2 Admin, 2 Staff, 11 Student)
    //   - >=30 reservations across the 6 seeded spaces and 6 dates
    //   - >=20 audit logs (CREATE for every reservation + APPROVE/REJECT trail)
    //
    // All demo accounts use the @eia.edu.co domain so the frontend's EIA-only
    // login validation accepts them. Password for every user: Sala1234.
    public static class DbSeeder
    {
        private const string DefaultPassword = "Sala1234";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var context     = services.GetRequiredService<ApplicationDbContext>();

            await EnsureRolesAsync(roleManager);
            await SeedUsersAsync(userManager);
            await SeedReservationsAsync(context, userManager);
            await SeedAuditLogsAsync(context, userManager);
        }

        private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in new[] { "Admin", "Staff", "Student" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
        {
            var demoUsers = new (string Email, string FullName, string Program, string Role)[]
            {
                ("admin@eia.edu.co",       "Administrador Principal",   "Administración",          "Admin"),
                ("admin2@eia.edu.co",      "Administradora Secundaria", "Administración",          "Admin"),
                ("staff1@eia.edu.co",      "Carlos Pérez",              "Operaciones",             "Staff"),
                ("staff2@eia.edu.co",      "María Restrepo",            "Operaciones",             "Staff"),
                ("ana.gomez@eia.edu.co",   "Ana Gómez",                 "Ingeniería de Sistemas",  "Student"),
                ("juan.lopez@eia.edu.co",  "Juan López",                "Ingeniería de Sistemas",  "Student"),
                ("lucia.diaz@eia.edu.co",  "Lucía Díaz",                "Ingeniería Electrónica",  "Student"),
                ("pedro.ruiz@eia.edu.co",  "Pedro Ruiz",                "Ingeniería Electrónica",  "Student"),
                ("sara.castro@eia.edu.co", "Sara Castro",               "Mecatrónica",             "Student"),
                ("diego.mora@eia.edu.co",  "Diego Mora",                "Mecatrónica",             "Student"),
                ("paula.rios@eia.edu.co",  "Paula Ríos",                "Ingeniería Civil",        "Student"),
                ("andres.vega@eia.edu.co", "Andrés Vega",               "Ingeniería Civil",        "Student"),
                ("camila.soto@eia.edu.co", "Camila Soto",               "Administración",          "Student"),
                ("felipe.cano@eia.edu.co", "Felipe Cano",               "Administración",          "Student"),
                ("laura.mejia@eia.edu.co", "Laura Mejía",               "Ingeniería Biomédica",    "Student"),
            };

            foreach (var u in demoUsers)
            {
                if (await userManager.FindByEmailAsync(u.Email) is not null) continue;

                var user = new ApplicationUser
                {
                    UserName = u.Email,
                    Email = u.Email,
                    EmailConfirmed = true,
                    FullName = u.FullName,
                    Program = u.Program,
                    IsBlocked = false,
                    NoShowCount = 0,
                };

                var result = await userManager.CreateAsync(user, DefaultPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"No se pudo crear el usuario demo {u.Email}: " +
                        string.Join("; ", result.Errors.Select(e => e.Description)));

                await userManager.AddToRoleAsync(user, u.Role);
            }
        }

        private static async Task SeedReservationsAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            if (await context.Reservations.AnyAsync()) return;

            var admins = await userManager.GetUsersInRoleAsync("Admin");
            var students = await userManager.GetUsersInRoleAsync("Student");
            if (admins.Count == 0 || students.Count == 0) return;
            var admin = admins.First();

            var spaceIds = new[]
            {
                ApplicationDbContext.SpaceIdSalaA101,
                ApplicationDbContext.SpaceIdLabComputoB205,
                ApplicationDbContext.SpaceIdAuditorio,
                ApplicationDbContext.SpaceIdSalaReunionesC301,
                ApplicationDbContext.SpaceIdCanchaDeportiva,
                ApplicationDbContext.SpaceIdLabElectronicaD110,
            };

            var slots = new (int StartHour, int EndHour)[]
            {
                (8, 10), (10, 12), (13, 15), (15, 17), (17, 19),
            };

            var statusCycle = new[]
            {
                ReservationStatus.Approved,
                ReservationStatus.Approved,
                ReservationStatus.Pending,
                ReservationStatus.Approved,
                ReservationStatus.Rejected,
                ReservationStatus.Cancelled,
            };

            var purposes = new[]
            {
                "Clase magistral de algoritmos",
                "Laboratorio de circuitos analógicos",
                "Reunión del semillero de robótica",
                "Charla con egresados",
                "Práctica de redes y telecomunicaciones",
                "Tutoría grupal de cálculo",
                "Presentación final de proyecto",
                "Entrenamiento del equipo deportivo",
                "Comité académico semestral",
                "Defensa de tesis de grado",
            };

            var today = DateTime.UtcNow.Date;
            var rng = new Random(20260517);
            var reservations = new List<Reservation>(30);
            var index = 0;

            // 6 days x 6 spaces = 36 distinct (space, date, slot) combos
            // Each combo gets a unique slot rotation, so no Approved/Pending overlap.
            for (int day = 0; day < 6 && index < 30; day++)
            {
                var date = today.AddDays(day - 2); // spread: past + future
                for (int s = 0; s < spaceIds.Length && index < 30; s++)
                {
                    var slot = slots[index % slots.Length];
                    var user = students[index % students.Count];
                    var status = statusCycle[index % statusCycle.Length];
                    var purpose = purposes[index % purposes.Length];

                    var reservation = new Reservation
                    {
                        SpaceId = spaceIds[s],
                        UserId = user.Id,
                        Date = date,
                        StartTime = new TimeSpan(slot.StartHour, 0, 0),
                        EndTime = new TimeSpan(slot.EndHour, 0, 0),
                        Purpose = purpose,
                        AttendeeCount = rng.Next(3, 20),
                        Status = status,
                        CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 10)),
                    };

                    if (status == ReservationStatus.Approved)
                    {
                        reservation.ApprovedByUserId = admin.Id;
                        reservation.ApprovedAt = reservation.CreatedAt.AddHours(rng.Next(1, 24));
                    }
                    else if (status == ReservationStatus.Rejected)
                    {
                        reservation.ApprovedByUserId = admin.Id;
                        reservation.RejectionReason = "Conflicto con mantenimiento programado";
                    }

                    reservations.Add(reservation);
                    index++;
                }
            }

            await context.Reservations.AddRangeAsync(reservations);
            await context.SaveChangesAsync();
        }

        private static async Task SeedAuditLogsAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            if (await context.AuditLogs.CountAsync() >= 20) return;

            var admins = await userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count == 0) return;
            var admin = admins.First();

            var reservations = await context.Reservations
                .OrderBy(r => r.CreatedAt)
                .Take(20)
                .ToListAsync();
            if (reservations.Count == 0) return;

            var logs = new List<AuditLog>();
            foreach (var reservation in reservations)
            {
                logs.Add(new AuditLog
                {
                    ReservationId = reservation.Id,
                    UserId = reservation.UserId,
                    Action = "CREATE",
                    Details = "Reserva creada por el usuario",
                    Timestamp = reservation.CreatedAt,
                    PreviousStatus = string.Empty,
                    NewStatus = ReservationStatus.Pending.ToString(),
                });

                if (logs.Count >= 20) break;

                if (reservation.Status == ReservationStatus.Approved ||
                    reservation.Status == ReservationStatus.Rejected)
                {
                    logs.Add(new AuditLog
                    {
                        ReservationId = reservation.Id,
                        UserId = admin.Id,
                        Action = reservation.Status == ReservationStatus.Approved ? "APPROVE" : "REJECT",
                        Details = reservation.Status == ReservationStatus.Approved
                            ? "Reserva aprobada por el administrador"
                            : $"Reserva rechazada: {reservation.RejectionReason}",
                        Timestamp = reservation.ApprovedAt ?? reservation.CreatedAt.AddHours(2),
                        PreviousStatus = ReservationStatus.Pending.ToString(),
                        NewStatus = reservation.Status.ToString(),
                    });

                    if (logs.Count >= 20) break;
                }
            }

            await context.AuditLogs.AddRangeAsync(logs);
            await context.SaveChangesAsync();
        }
    }
}
