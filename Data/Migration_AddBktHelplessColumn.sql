-- Migration: Add BKT Helpless tracking column to progress table
-- Run once against the Supabase database.

ALTER TABLE public.progress
    ADD COLUMN IF NOT EXISTS consecutive_low_probability INTEGER NOT NULL DEFAULT 0;
