-- Drop existing table if it exists with wrong column names
DROP TABLE IF EXISTS public.refresh_tokens;

-- Create refresh_tokens table with camelCase column names to match C# entity
CREATE TABLE public.refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token TEXT NOT NULL,
    userId UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    expiresAt TIMESTAMP WITH TIME ZONE NOT NULL,
    createdAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    revokedAt TIMESTAMP WITH TIME ZONE
);

-- Create index on token for faster lookups
CREATE INDEX idx_refresh_tokens_token ON public.refresh_tokens(token);

-- Create index on userId for faster user lookups
CREATE INDEX idx_refresh_tokens_userId ON public.refresh_tokens(userId);

-- Enable Row Level Security (optional, if you want to use RLS)
ALTER TABLE public.refresh_tokens ENABLE ROW LEVEL SECURITY;

-- Create policy to allow users to see their own refresh tokens (optional)
-- DROP POLICY IF EXISTS "Users can view own refresh tokens" ON public.refresh_tokens;
-- CREATE POLICY "Users can view own refresh tokens" ON public.refresh_tokens
--     FOR SELECT USING (auth.uid()::text = user_id::text);

-- Create policy to allow users to insert their own refresh tokens (optional)
-- DROP POLICY IF EXISTS "Users can insert own refresh tokens" ON public.refresh_tokens;
-- CREATE POLICY "Users can insert own refresh tokens" ON public.refresh_tokens
--     FOR INSERT WITH CHECK (auth.uid()::text = user_id::text);

-- Create policy to allow users to update their own refresh tokens (optional)
-- DROP POLICY IF EXISTS "Users can update own refresh tokens" ON public.refresh_tokens;
-- CREATE POLICY "Users can update own refresh tokens" ON public.refresh_tokens
--     FOR UPDATE USING (auth.uid()::text = user_id::text);
