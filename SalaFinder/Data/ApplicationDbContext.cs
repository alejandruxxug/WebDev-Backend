using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalaFinder.Models;

namespace SalaFinder.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Space> Spaces { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.ApprovedBy)
                .WithMany()
                .HasForeignKey(r => r.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Reservation)
                .WithMany(r => r.AuditLogs)
                .HasForeignKey(a => a.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);

            SeedRoles(modelBuilder);
            SeedSpaces(modelBuilder);
        }

        // === IDs estaticos para HasData ===
        // EF Core 10 trata PendingModelChangesWarning como error, asi que las
        // Guids del seed deben ser deterministicas entre builds. Si se generaran
        // con Guid.NewGuid() el snapshot del modelo cambiaria cada compilacion.
        public static readonly Guid SpaceIdSalaA101          = new("11111111-1111-1111-1111-111111111111");
        public static readonly Guid SpaceIdLabComputoB205    = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid SpaceIdAuditorio         = new("33333333-3333-3333-3333-333333333333");
        public static readonly Guid SpaceIdSalaReunionesC301 = new("44444444-4444-4444-4444-444444444444");
        public static readonly Guid SpaceIdCanchaDeportiva   = new("55555555-5555-5555-5555-555555555555");
        public static readonly Guid SpaceIdLabElectronicaD110 = new("66666666-6666-6666-6666-666666666666");

        private void SeedRoles(ModelBuilder modelBuilder)
        {
            // ConcurrencyStamp se fija a un valor estatico para que el modelo
            // sea deterministico entre builds (EF Core 10 trata el warning
            // PendingModelChangesWarning como error).
            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole { Id = "1", Name = "Admin",   NormalizedName = "ADMIN",   ConcurrencyStamp = "static-stamp-admin" },
                new IdentityRole { Id = "2", Name = "Staff",   NormalizedName = "STAFF",   ConcurrencyStamp = "static-stamp-staff" },
                new IdentityRole { Id = "3", Name = "Student", NormalizedName = "STUDENT", ConcurrencyStamp = "static-stamp-student" }
            );
        }

        private void SeedSpaces(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Space>().HasData(
                new Space
                {
                    Id = SpaceIdSalaA101, Name = "Sala A-101", Type = "Classroom",
                    Capacity = 30, Building = "Edificio A",
                    Resources = "Proyector,Pizarrón,WiFi",
                    AllowedPrograms = "Ingeniería,Sistemas,Administración",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = SpaceIdLabComputoB205, Name = "Lab Cómputo B-205", Type = "Lab",
                    Capacity = 25, Building = "Edificio B",
                    Resources = "Computadoras,Proyector,WiFi",
                    AllowedPrograms = "Sistemas,Ingeniería",
                    RequiresApproval = true, IsActive = true
                },
                new Space
                {
                    Id = SpaceIdAuditorio, Name = "Auditorio Principal", Type = "Auditorium",
                    Capacity = 200, Building = "Edificio C",
                    Resources = "Audio,Video,Proyector,WiFi",
                    AllowedPrograms = "Todos",
                    RequiresApproval = true, IsActive = true
                },
                new Space
                {
                    Id = SpaceIdSalaReunionesC301, Name = "Sala Reuniones C-301", Type = "MeetingRoom",
                    Capacity = 12, Building = "Edificio C",
                    Resources = "TV,Pizarrón,WiFi",
                    AllowedPrograms = "Todos",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = SpaceIdCanchaDeportiva, Name = "Cancha Deportiva", Type = "Court",
                    Capacity = 20, Building = "Complejo Deportivo",
                    Resources = "Vestidores,Iluminación",
                    AllowedPrograms = "Todos",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = SpaceIdLabElectronicaD110, Name = "Lab Electrónica D-110", Type = "Lab",
                    Capacity = 20, Building = "Edificio D",
                    Resources = "Osciloscopios,Multímetros,Soldadores,WiFi",
                    AllowedPrograms = "Ingeniería Electrónica,Mecatrónica",
                    RequiresApproval = true, IsActive = true
                }
            );
        }
    }
}
