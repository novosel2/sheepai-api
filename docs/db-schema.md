# Database Schema

Three tables. Snake_case naming is handled automatically by `UseSnakeCaseNamingConvention()` in EF Core — do not set column names manually.

## chats

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK, `gen_random_uuid()` |
| `created_at` | `timestamp` | default `now()` |
| `is_urgent` | `boolean` | default `false` — set automatically by urgency logic |
| `is_admin_taken` | `boolean` | default `false` — set to `true` when admin takes over |
| `last_message_at` | `timestamp` | updated on every new message |

## chat_messages

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK, `gen_random_uuid()` |
| `chat_id` | `uuid` | FK → `chats.id` ON DELETE CASCADE |
| `created_at` | `timestamp` | default `now()` |
| `role` | `text` | `'user'`, `'assistant'`, or `'admin'` |
| `content` | `text` | message body |

## files

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK, `gen_random_uuid()` |
| `created_at` | `timestamp` | default `now()` |
| `name` | `text` | display name shown in admin UI |
| `anthropic_file_id` | `text` | unique — the `file_id` returned by Anthropic Files API on upload |

## Raw SQL

```sql
-- chats
create table chats (
  id              uuid primary key default gen_random_uuid(),
  created_at      timestamp default now(),
  is_urgent       boolean default false,
  is_admin_taken  boolean default false,
  last_message_at timestamp default now()
);

-- chat_messages
create table chat_messages (
  id          uuid primary key default gen_random_uuid(),
  chat_id     uuid references chats(id) on delete cascade,
  created_at  timestamp default now(),
  role        text not null,
  content     text not null
);

-- files
create table files (
  id                 uuid primary key default gen_random_uuid(),
  created_at         timestamp default now(),
  name               text not null,
  anthropic_file_id  text unique not null
);
```
