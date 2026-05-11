-- Migration: Add HBDA v2 columns to submissions table
-- Run once against the Supabase database.

ALTER TABLE public.submissions
    ADD COLUMN IF NOT EXISTS paste_detected       BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS task_elapsed_seconds DOUBLE PRECISION NOT NULL DEFAULT 0;
