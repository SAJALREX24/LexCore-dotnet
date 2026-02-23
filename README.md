# LexCore - Legal Practice Management System

A multi-tenant SaaS platform for Indian law firms to manage cases, clients, documents, hearings, billing, and team collaboration.

## Tech Stack

- **Backend**: ASP.NET Core 8 Web API (C#)
- **ORM**: Entity Framework Core 8 with PostgreSQL provider
- **Database**: PostgreSQL 16
- **Cache**: Redis 7
- **Background Jobs**: Hangfire with Redis storage
- **Authentication**: JWT Bearer tokens
- **Email**: MailKit (SMTP)
- **File Storage**: Local filesystem / AWS S3
- **PDF Generation**: QuestPDF
- **API Documentation**: Swagger

## Quick Start

### Prerequisites

- Docker and Docker Compose
- Git

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd LexCore
   ```

2. **Create environment file**
   ```bash
   cp .env.example .env
   ```

3. **Configure environment variables**
   
   Open `.env` and fill in the required values:
   - `POSTGRES_PASSWORD` - Database password
   - `JWT_SECRET` - Secret key for JWT (min 64 characters)
   - `EMAIL_*` - Gmail SMTP settings (optional for dev)

4. **Start the application**
   ```bash
   docker compose up --build
   ```

5. **Access the API**
   - Swagger UI: http://localhost:5000/swagger
   - Hangfire Dashboard: http://localhost:5000/hangfire (requires SuperAdmin login)

### Default Users (Development)

The database is automatically seeded with test users:

| Role | Email | Password |
|------|-------|----------|
| Super Admin | superadmin@lexcore.in | SuperAdmin@1234 |
| Firm Admin | admin@lexcore.in | Admin@1234 |
| Lawyer | lawyer@lexcore.in | Lawyer@1234 |
| Client | client@lexcore.in | Client@1234 |

### Running Migrations (Manual)

If you need to run migrations manually:

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Run from the API project directory
cd src/LexCore.API
dotnet ef migrations add InitialCreate --project ../LexCore.Infrastructure
dotnet ef database update --project ../LexCore.Infrastructure
```

## API Modules

### Authentication (`/api/auth`)
- `POST /register-firm` - Register new firm + admin
- `POST /login` - Login and get JWT token
- `POST /refresh` - Refresh access token
- `POST /logout` - Invalidate refresh token
- `POST /forgot-password` - Request password reset
- `POST /reset-password` - Reset password with token
- `GET /verify-email` - Verify email address
- `POST /invite` - Invite user to firm (FirmAdmin)
- `POST /accept-invite` - Accept invitation

### Users (`/api/users`)
- `GET /` - List users in firm
- `GET /{id}` - Get user details
- `PATCH /{id}` - Update user
- `DELETE /{id}` - Soft delete user

### Firms (`/api/firms`)
- `GET /me` - Get current firm
- `PATCH /me` - Update firm details
- `POST /me/logo` - Upload firm logo

### Cases (`/api/cases`)
- `POST /` - Create case
- `GET /` - List cases (paginated, filterable)
- `GET /{id}` - Get case details
- `PATCH /{id}` - Update case
- `PATCH /{id}/status` - Change case status
- `DELETE /{id}` - Soft delete case
- `POST /{id}/lawyers` - Assign lawyer
- `DELETE /{id}/lawyers/{lawyerId}` - Remove lawyer
- `POST /{id}/clients` - Assign client
- `DELETE /{id}/clients/{clientId}` - Remove client
- `GET /{id}/timeline` - Get case audit trail
- `POST /{id}/notes` - Add case note

### Documents (`/api/documents`)
- `POST /upload` - Upload document
- `GET /case/{caseId}` - List case documents
- `GET /{id}/download` - Download document
- `PATCH /{id}` - Update document metadata
- `DELETE /{id}` - Soft delete document
- `POST /{id}/version` - Upload new version
- `GET /{id}/versions` - Get version history

### Hearings (`/api/hearings`)
- `POST /` - Schedule hearing
- `GET /` - List hearings (filterable)
- `GET /calendar` - Calendar view
- `GET /{id}` - Get hearing details
- `PATCH /{id}` - Update hearing
- `PATCH /{id}/status` - Update status
- `DELETE /{id}` - Soft delete hearing

### Chat (`/api/chat`)
- `POST /{caseId}` - Send message
- `GET /{caseId}` - Get messages

### Billing (`/api/billing`)
- `POST /subscribe` - Create subscription
- `POST /webhook` - Razorpay webhook
- `GET /subscription` - Get current subscription
- `POST /invoices` - Create invoice
- `GET /invoices` - List invoices
- `GET /invoices/{id}` - Get invoice details
- `PATCH /invoices/{id}/send` - Send invoice to client
- `GET /invoices/{id}/pdf` - Download invoice PDF

### Notifications (`/api/notifications`)
- `GET /` - Get user notifications
- `PATCH /{id}/read` - Mark as read
- `PATCH /read-all` - Mark all as read

### Analytics (`/api/analytics`) - FirmAdmin only
- `GET /overview` - Dashboard overview
- `GET /cases` - Cases breakdown
- `GET /revenue` - Revenue analytics
- `GET /lawyers` - Lawyer performance
- `GET /hearings` - Hearing statistics

### Audit (`/api/audit`) - FirmAdmin only
- `GET /` - List audit logs

## Architecture

```
LexCore/
├── src/
│   ├── LexCore.API/           # Controllers, Middleware, Program.cs
│   ├── LexCore.Application/   # Services, DTOs, Interfaces, Validators
│   ├── LexCore.Domain/        # Entities, Enums
│   └── LexCore.Infrastructure/ # DbContext, Repositories, Services, Jobs
├── docker-compose.yml
├── Dockerfile
├── .env.example
└── README.md
```

## Multi-Tenancy

LexCore uses a shared database with row-level tenant isolation:
- Every table has a `FirmId` column
- Every query filters by `FirmId`
- Tenant context extracted from JWT claims via `TenantMiddleware`

## Security

- JWT access tokens expire in 15 minutes
- Refresh tokens expire in 7 days and rotate on each use
- Passwords hashed with BCrypt (work factor 12)
- Rate limiting on auth endpoints
- Role-based authorization on all endpoints
- Soft deletes for data retention

## License

Proprietary - All rights reserved
