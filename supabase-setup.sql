create extension if not exists "pgcrypto";

create table if not exists public.users (
  id uuid primary key default gen_random_uuid(),
  email text not null unique,
  password_hash text not null,
  full_name text not null,
  stripe_customer_id text,
  created_at timestamptz not null default now()
);

create table if not exists public.subscriptions (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references public.users(id) on delete cascade,
  tier text not null check (tier in ('Promotion', 'Gold', 'Diamond')),
  duration text not null check (duration in ('Monthly', 'SixMonths', 'Yearly')),
  stripe_subscription_id text,
  status text not null default 'Inactive',
  price numeric(18, 2) not null,
  currency char(3) not null default 'USD',
  starts_at timestamptz not null default now(),
  ends_at timestamptz not null,
  is_auto_renew_enabled boolean not null default true,
  last_expiration_notification_at timestamptz
);

create index if not exists ix_users_email on public.users (email);
create index if not exists ix_subscriptions_user_id on public.subscriptions (user_id);
create index if not exists ix_subscriptions_status_ends_at on public.subscriptions (status, ends_at);

alter table public.users enable row level security;
alter table public.subscriptions enable row level security;

revoke all on table public.users from anon, authenticated;
revoke all on table public.subscriptions from anon, authenticated;

grant all on table public.users to service_role;
grant all on table public.subscriptions to service_role;

notify pgrst, 'reload schema';
