using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalaFinder.Models;

namespace SalaFinder.Data
{
    // ============================================================
    // === SEED DATA (runtime) ====================================
    // ============================================================
// Pobla la DB con usuarios demo y reservas de ejemplo despues
    // de que db.Database.Migrate() corra en Program.cs.
    // Es idempotente: si los datos ya existen, no hace nada.
    // Los roles (Admin/Staff/Student) y los Spaces ya se siembran
    // via HasData en ApplicationDbContext.OnModelCreating.
    // ============================================================
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var context     = services.GetRequiredService<ApplicationDbContext>();

            await SeedUsersAsync(userManager);
            await SeedReservationsAsync(context, userManager);
        }

        // === SEED: usuarios demo ===
        // Crea 1 Admin, 1 Staff y 2 Students con contrasenas conocidas.
        // Credenciales (solo desarrollo):
        //   admin@salafinder.com    / Admin1234
        //   staff@salafinder.com    / Staff1234
        //   student1@salafinder.com / Student1234
        //   student2@salafinder.com / Student1234
        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
        {
            var demoUsers = new (string Email, string Password, string FullName, string Program, string Role)[]
            {
                ("admin@salafinder.com",    "Admin1234",   "Administrador General", "Administración", "Admin"),
                ("staff@salafinder.com",    "Staff1234",   "Coordinador Staff",     "Administración", "Staff"),
                ("student1@salafinder.com", "Student1234", "Ana Estudiante",        "Ingeniería",     "Student"),
                ("student2@salafinder.com", "Student1234", "Luis Estudiante",       "Sistemas",       "Student")
            };

            foreach (var u in demoUsers)
            {
                if (await userManager.FindByEmailAsync(u.Email) != null) continue;

                var user = new ApplicationUser
                {
                    UserName = u.Email,
                    Email = u.Email,
                    EmailConfirmed = true,
                    FullName = u.FullName,
                    Program = u.Program,
                    IsBlocked = false,
                    NoShowCount = 0
                };

                var result = await userManager.CreateAsync(user, u.Password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, u.Role);
            }
        }

        // === SEED: reservas demo ===
        // Inserta 3 reservas de ejemplo (Approved / Pending / NoShow)
        // para que la UI tenga datos visibles desde el primer arranque.
        // Solo corre si la tabla Reservations esta vacia.
        private static async Task SeedReservationsAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            if (await context.Reservations.AnyAsync()) return;

            var student1 = await userManager.FindByEmailAsync("student1@salafinder.com");
            var student2 = await userManager.FindByEmailAsync("student2@salafinder.com");
            if (student1 == null || student2 == null) return;

            var today = DateTime.UtcNow.Date;

            context.Reservations.AddRange(
                new Reservation
                {
                    SpaceId = ApplicationDbContext.SpaceIdSalaA101,
                    UserId = student1.Id,
                    Date = today.AddDays(1),
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime   = new TimeSpan(11, 0, 0),
                    Purpose = "Sesión de estudio grupal",
                    AttendeeCount = 8,
                    Status = ReservationStatus.Approved,
                    CreatedAt = DateTime.UtcNow
                },
                new Reservation
                {
                    SpaceId = ApplicationDbContext.SpaceIdLabComputoB205,
                    UserId = student2.Id,
                    Date = today.AddDays(2),
                    StartTime = new TimeSpan(14, 0, 0),
                    EndTime   = new TimeSpan(16, 0, 0),
                    Purpose = "Práctica de laboratorio",
                    AttendeeCount = 15,
                    Status = ReservationStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                },
                new Reservation
                {
                    SpaceId = ApplicationDbContext.SpaceIdSalaReunionesC301,
                    UserId = student1.Id,
                    Date = today.AddDays(-3),
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime   = new TimeSpan(12, 0, 0),
                    Purpose = "Reunión de equipo",
                    AttendeeCount = 6,
                    Status = ReservationStatus.NoShow,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                }
            );

            await context.SaveChangesAsync();
        }
    }
}
