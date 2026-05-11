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

        private void SeedRoles(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" },
                new IdentityRole { Id = "2", Name = "Staff", NormalizedName = "STAFF" },
                new IdentityRole { Id = "3", Name = "Student", NormalizedName = "STUDENT" }
            );
        }

        private void SeedSpaces(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Space>().HasData(
                new Space
                {
                    Id = 1, Name = "Sala A-101", Type = "Classroom",
                    Capacity = 30, Building = "Edificio A",
                    Resources = "Proyector,Pizarrón,WiFi",
                    AllowedPrograms = "Ingeniería,Sistemas,Administración",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = 2, Name = "Lab Cómputo B-205", Type = "Lab",
                    Capacity = 25, Building = "Edificio B",
                    Resources = "Computadoras,Proyector,WiFi",
                    AllowedPrograms = "Sistemas,Ingeniería",
                    RequiresApproval = true, IsActive = true
                },
                new Space
                {
                    Id = 3, Name = "Auditorio Principal", Type = "Auditorium",
                    Capacity = 200, Building = "Edificio C",
                    Resources = "Audio,Video,Proyector,WiFi",
                    AllowedPrograms = "Todos",
                    RequiresApproval = true, IsActive = true
                },
                new Space
                {
                    Id = 4, Name = "Sala Reuniones C-301", Type = "MeetingRoom",
                    Capacity = 12, Building = "Edificio C",
                    Resources = "TV,Pizarrón,WiFi",
                    AllowedPrograms = "Todos",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = 5, Name = "Cancha Deportiva", Type = "Court",
                    Capacity = 20, Building = "Complejo Deportivo",
                    Resources = "Vestidores,Iluminación",
                    AllowedPrograms = "Todos",
                    RequiresApproval = false, IsActive = true
                },
                new Space
                {
                    Id = 6, Name = "Lab Electrónica D-110", Type = "Lab",
                    Capacity = 20, Building = "Edificio D",
                    Resources = "Osciloscopios,Multímetros,Soldadores,WiFi",
                    AllowedPrograms = "Ingeniería Electrónica,Mecatrónica",
                    RequiresApproval = true, IsActive = true
                }
            );
        }
    }
}
