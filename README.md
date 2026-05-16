# SheepAI

AI assistant for the City of Split. Citizens and tourists can ask questions about city services and administrative procedures. City officials get an admin dashboard to manage documents and conversations.

## Features

**Public**
- Anonymous chat — no registration required
- AI answers based on uploaded city documents
- Automatic urgency detection — flags chats that need human attention
- Auto-generated chat name and summary

**Admin**
- Login-protected dashboard
- Upload and manage city documents (PDF, TXT, MD)
- View all chat sessions with names, summaries, and urgency flags
- Take over any chat and respond to citizens manually
- Finished chats tracked separately

## Stack

- **Backend:** ASP.NET Core 10, C#
- **Database:** PostgreSQL (Supabase) via EF Core
- **Cache:** Redis
- **AI:** Anthropic Claude API with Files API and prompt caching
- **Auth:** JWT (7-day tokens)

## Project Structure

```
src/
├── SheepAI.Domain/          # Entities, exceptions
├── SheepAI.Application/     # Interfaces, DTOs, options
├── SheepAI.Infrastructure/  # EF Core, Claude, Redis
└── SheepAI.API/             # Controllers, middleware, DI
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL (or a Supabase project)
- Redis
- Anthropic API key

### Setup

1. Copy the example env file and fill in your values:

```bash
cp .env.example .env
```

2. Apply database migrations:

```bash
dotnet ef database update --project src/SheepAI.Infrastructure --startup-project src/SheepAI.API
```

3. Run the API:

```bash
dotnet run --project src/SheepAI.API
```

### Environment Variables

| Variable | Description |
|---|---|
| `JWT_SECRET` | Strong random secret (`openssl rand -base64 64`) |
| `JWT_ISSUER` | Token issuer URL |
| `JWT_AUDIENCE` | Token audience |
| `JWT_EXPIRES_MINUTES` | Token lifetime in minutes (default 10080 = 7 days) |
| `CLAUDE_API_KEY` | Anthropic API key |
| `CLAUDE_DEFAULT_MODEL` | Model to use (e.g. `claude-opus-4-6`) |
| `CLAUDE_MAX_TOKENS` | Max tokens per response |
| `DATABASE_CONNECTION_STRING` | Npgsql connection string |
| `REDIS_CONNECTION` | Redis connection string |
| `CORS_ALLOWED_ORIGINS_0` | Allowed CORS origin (add more with `_1`, `_2`, …) |
| `RATE_LIMIT_PERMIT` | Max requests per window per IP |
| `RATE_LIMIT_WINDOW_SECONDS` | Rate limit window in seconds |

## API

Key endpoints:

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/chats` | — | Create a new chat session |
| `POST` | `/api/chats/{id}/messages` | — | Send a message, get AI response |
| `GET` | `/api/chats/{id}/messages` | — | Get chat history |
| `GET` | `/api/chats/{id}/status` | — | Poll for name, summary, admin takeover |
| `POST` | `/api/chats/{id}/finish` | — | Mark chat as finished |
| `POST` | `/api/admin/login` | — | Admin login |
| `POST` | `/api/admin/logout` | JWT | Admin logout |
| `GET` | `/api/admin/chats` | JWT | All chats (urgent first) |
| `POST` | `/api/admin/chats/{id}/take` | JWT | Take over a chat |
| `POST` | `/api/admin/chats/{id}/messages` | JWT | Send admin message |
| `GET` | `/api/admin/files` | JWT | List uploaded documents |
| `POST` | `/api/admin/files` | JWT | Upload a document |
| `DELETE` | `/api/admin/files/{id}` | JWT | Delete a document |
| `GET` | `/health` | — | Health check (DB + Redis) |

Full OpenAPI spec available at `/swagger` in development.
