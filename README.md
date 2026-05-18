# SalaFinder API

API REST en **ASP.NET Core 10** para la reserva de aulas, laboratorios y canchas universitarias. Maneja autenticación con **ASP.NET Identity + JWT**, persistencia con **EF Core 10 + SQL Server**, control de roles (Admin / Staff / Student) y un flujo de aprobación de reservas con auditoría y política de no-shows.

> Para una guía detallada del comportamiento y reglas de negocio orientada a sustentación oral, ver [`../WebDev-SalaFinder/presentation.md`](../WebDev-SalaFinder/presentation.md).

## Despliegue en producción

- API: **https://salafindereia.azurewebsites.net** (Azure App Service Linux, B1)
- BD: **Azure SQL Database serverless** (`salafinderdb.database.windows.net`)
- Frontend que la consume: **https://web-dev-sala-finder.vercel.app**

## Stack

- .NET 10 / ASP.NET Core 10 (`net10.0`)
- Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`)
- ASP.NET Identity + JWT Bearer (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`)
- SQL Server 2022 (vía Docker en local) / Azure SQL Database serverless (producción)
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

`appsettings.json` está **gitignored** (contiene secretos). Copia la plantilla y edita con tu password:

```bash
cp SalaFinder/appsettings.Example.json SalaFinder/appsettings.json
```

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=SalaFinderDB;User Id=sa;Password=Tu_Password_Aqui;TrustServerCertificate=True;Encrypt=False;"
}
```

`appsettings.Example.json` queda versionado como referencia con placeholders.

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

15 usuarios `@eia.edu.co`, todos con password `Sala1234`:

- **2 Admin** — `admin@eia.edu.co`, `admin2@eia.edu.co`
- **2 Staff** — `staff1@eia.edu.co`, `staff2@eia.edu.co`
- **11 Student** — `student1@eia.edu.co` … `student11@eia.edu.co`

Más **30 reservas** distribuidas en 6 fechas × 6 espacios con mezcla de estados (Pending / Approved / Rejected / Cancelled / NoShow) y **20 registros de auditoría** que reflejan las transiciones. El seeder es idempotente: chequea existencia antes de insertar.

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

## Producción — Azure App Service + Azure SQL Serverless

### Deploy

Se publica desde Rider (plugin **Azure Toolkit for Rider** → click derecho en `SalaFinder` → **Azure** → **Publish**). Empaqueta la build de Release y la sube al App Service vía Zip Deploy. Ese flujo sube los archivos del working tree (incluyendo `appsettings.json` con credenciales locales), por eso **las credenciales reales de producción deben vivir en Azure App Settings, no en el archivo**.

### Configuración (App Service → Configuration → Application settings)

Linux usa la sintaxis de doble guion bajo:

| Key | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | Connection string completa de Azure SQL (con `Encrypt=True`) |
| `Jwt__Key` | Clave HMAC (idéntica a la de `appsettings.json` local) |
| `Jwt__Issuer` | `SalaFinderApi` |
| `Jwt__Audience` | `SalaFinderUsers` |
| `Cors__AllowedOrigins__0` | `https://web-dev-sala-finder.vercel.app` |
| `Cors__AllowedOrigins__1` | (opcional) `http://localhost:5173` |

### Firewall de Azure SQL

En el **SQL Server** (no la BD) → **Security → Networking** → activar **"Allow Azure services and resources to access this server"**. Sin esto, el App Service no logra abrir la conexión TLS y el proceso muere al arrancar.

### Resiliencia para auto-pause (Azure SQL serverless)

Azure SQL Serverless pausa la BD tras inactividad. El primer request post-pausa tarda 30–60s y mientras tanto el servidor devuelve "database not currently available". `Program.cs` configura:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString,
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 8,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));
```

Y la `Database.Migrate()` de arranque se envuelve en la execution strategy de EF Core para reintentar de forma transparente:

```csharp
var strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    db.Database.Migrate();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
});
```

### CORS

El middleware se registra leyendo orígenes de configuración y se monta **antes** de `UseAuthentication` (para que el preflight `OPTIONS` no exija JWT):

```csharp
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
    allowedOrigins = new[] { "http://localhost:5173" };  // fallback de desarrollo

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ...
app.UseCors();
app.UseAuthentication();
```

## Hecho por

- Sebastian Higuita
- Alejandro Urrego
