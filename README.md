# SalaFinder API

API REST en **ASP.NET Core 10** para la reserva de aulas, laboratorios y canchas universitarias. Maneja autenticación con **ASP.NET Identity + JWT**, persistencia con **EF Core 10 + SQL Server**, control de roles (Admin / Staff / Student) y un flujo de aprobación de reservas con auditoría y política de no-shows.

## Stack

- .NET 10 / ASP.NET Core 10 (`net10.0`)
- Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`)
- ASP.NET Identity + JWT Bearer (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`)
- SQL Server 2022 (vía Docker en local)
- Scalar para la UI de OpenAPI

## Prerequisitos

- .NET SDK **10.0.x**
- Docker (para correr SQL Server localmente)
- `dotnet-ef` global tool — `dotnet tool install --global dotnet-ef` (si todavía no la tienes)

## Setup

### 1. Levantar SQL Server en Docker

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Tu_Password_Aqui" \
  -p 1433:1433 --name sqlserver2022 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Configurar la cadena de conexión

Edita `SalaFinder/appsettings.json` con el password que usaste arriba:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=SalaFinderDB;User Id=sa;Password=Tu_Password_Aqui;TrustServerCertificate=True;Encrypt=False;"
}
```

### 3. Restaurar paquetes y correr

```bash
dotnet restore SalaFinder.sln
dotnet run --project SalaFinder
```

Al arrancar, `Program.cs` ejecuta `db.Database.Migrate()` y luego `DbSeeder.SeedAsync()`, así que la BD `SalaFinderDB` se crea y se puebla sola en el primer arranque.

La UI de Scalar queda en **https://localhost:{puerto}/scalar/v1** (puerto definido en `launchSettings.json`).

## Migraciones EF Core

Las migraciones corren automáticamente al iniciar la app. Para operarlas manualmente:

```bash
# Crear una nueva migración
dotnet ef migrations add NombreDeLaMigracion --project SalaFinder

# Aplicar las migraciones pendientes a la BD
dotnet ef database update --project SalaFinder

# Quitar la última migración (si todavía no se aplicó)
dotnet ef migrations remove --project SalaFinder
```

## Datos sembrados

### Vía `HasData` (en la migración inicial)

- **Roles**: `Admin`, `Staff`, `Student`
- **Spaces**: 6 espacios con Guids fijos (`11111111-…`, `22222222-…`, etc.) — ver `ApplicationDbContext.SpaceIdSalaA101` y similares.

### Vía `DbSeeder` (en runtime, después de migrar)

| Email | Password | Rol |
|---|---|---|
| `admin@salafinder.com` | `Admin1234` | Admin |
| `staff@salafinder.com` | `Staff1234` | Staff |
| `student1@salafinder.com` | `Student1234` | Student |
| `student2@salafinder.com` | `Student1234` | Student |

Más 3 reservas de ejemplo (Approved / Pending / NoShow). El seeder es idempotente: no duplica si ya existen.

> Credenciales solo para desarrollo. **No** dejarlas en producción.

## Endpoints principales

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| `POST` | `/api/auth/register` | público | Crea un usuario con rol Student/Staff/Admin |
| `POST` | `/api/auth/login` | público | Devuelve un JWT (válido 8h) |
| `GET` | `/api/auth/me` | autenticado | Perfil del usuario actual |
| `GET` | `/api/spaces` | público | Lista espacios (filtros opcionales) |
| `GET` | `/api/spaces/{id}` | público | Detalle de un espacio |
| `POST` | `/api/spaces` | Admin | Crear espacio |
| `PUT` | `/api/spaces/{id}` | Admin | Editar espacio |
| `DELETE` | `/api/spaces/{id}` | Admin | Desactivar espacio (soft delete) |
| `GET` | `/api/reservations` | Admin/Staff | Listar todas las reservas |
| `GET` | `/api/reservations/my` | autenticado | Mis reservas |
| `POST` | `/api/reservations` | autenticado | Crear reserva |
| `PATCH` | `/api/reservations/{id}/status` | Admin | Aprobar / rechazar |
| `DELETE` | `/api/reservations/{id}` | dueño | Cancelar mi reserva |
| `POST` | `/api/reservations/{id}/no-show` | Admin | Marcar no-show |
| `GET` | `/api/reservations/audit` | Admin | Log de auditoría |
| `POST` | `/api/reservations/unblock-users` | Admin | Desbloquear usuarios con bloqueo vencido |

## Arquitectura

Capas: **Controllers → Interfaces → Services → ApplicationDbContext (EF Core)**.

- `Controllers/` traduce las excepciones tipadas de los servicios a códigos HTTP: `InvalidOperationException`→409, `ArgumentException`→400, `KeyNotFoundException`→404, `UnauthorizedAccessException`→403.
- `Services/` aplica las reglas de negocio (no se confía en la BD para validarlas).
- `Data/ApplicationDbContext.cs` extiende `IdentityDbContext<ApplicationUser>`. Usa **DataAnnotations** en los modelos para validación (`[Required]`, `[StringLength]`) y **Fluent API** en `OnModelCreating` para lo que las anotaciones no expresan: `DeleteBehavior.Restrict` en las FKs (para no perder auditoría al borrar) y `HasData` para sembrar roles + espacios.
- `Data/DbSeeder.cs` siembra usuarios y reservas demo en runtime (porque requieren hashing de password y FKs en tiempo de ejecución, cosas que `HasData` no puede hacer).

### Reglas de negocio principales (`ReservationService`)

1. Usuarios bloqueados (`IsBlocked && BlockedUntil > now`) no pueden reservar.
2. Se rechazan fechas pasadas, `StartTime >= EndTime`, y `AttendeeCount > Space.Capacity`.
3. La detección de conflictos solo considera reservas `Approved`/`Pending` del mismo espacio/día con intervalos solapados; ofrece hasta 3 horarios alternativos entre 07:00–22:00 en bloques de 30 min.
4. Si `Space.RequiresApproval == false`, la reserva nace como `Approved`; si no, como `Pending`.
5. Al marcar `NoShow` se incrementa `User.NoShowCount`; al llegar a 2, el usuario queda bloqueado por 7 días.
6. Solo el dueño puede cancelar su reserva; los admins rechazan vía el endpoint de status.

## Hecho por

- Sebastian Higuita
- Alejandro Urrego
