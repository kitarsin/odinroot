-- ============================================================
-- ODIN Scaffolding Hints — Behavioral State Seed
-- ============================================================
-- Convention (Option A):
--   diagnostic_category = BehaviorState name (e.g. 'PostFailureDisengagement')
--   skill_type = 'All' for hints that apply across all skills
--   tier 1 = validated exact strings
--   tier 2 = domain-focused escalation hints
--
-- Run this against your Supabase project after deploying the API.
-- Safe to re-run (INSERT ... WHERE NOT EXISTS pattern).
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- PostFailureDisengagement
-- Trigger: keyboard idle >= 120s after an error
-- ────────────────────────────────────────────────────────────

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'PostFailureDisengagement',
    'All',
    1,
    'Odin',
    'Looks like that last error stopped you cold. No worries that happens to everyone. Can you try changing just ONE thing in the line the error message is pointing to? Start small.',
    NULL,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'PostFailureDisengagement'
      AND tier = 1
);

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'PostFailureDisengagement',
    'All',
    2,
    'Odin',
    'That error message has a line number in it — go to that exact line and read what the code is doing step by step. Arrays start at index 0, not 1. Could that be the issue?',
    'Check: is your index within bounds? If your array has 5 elements, valid indices are 0–4.',
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'PostFailureDisengagement'
      AND tier = 2
);

-- ────────────────────────────────────────────────────────────
-- WheelSpinning
-- Trigger: >= 3 consecutive identical compiler errors, no structural code change
-- ────────────────────────────────────────────────────────────

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'WheelSpinning',
    'All',
    1,
    'Odin',
    'You''ve submitted the same code a few times now, and it''s still not working. Try something different - what part of the logic might be wrong? Write it out on scratch paper first.',
    NULL,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'WheelSpinning'
      AND tier = 1
);

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'WheelSpinning',
    'All',
    2,
    'Odin',
    'If the same error keeps appearing, the fix probably isn''t a small tweak — you may need to restructure part of your logic. Think about what the loop or array access is supposed to do, then rewrite that section from scratch.',
    'Tip: delete the broken section and rewrite it from a blank line. Sometimes starting fresh is faster than patching.',
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'WheelSpinning'
      AND tier = 2
);

-- ────────────────────────────────────────────────────────────
-- LowProgressTrialAndError
-- Trigger: TimeSinceLastSubmit < 6s AND only numeric/operator swaps
-- ────────────────────────────────────────────────────────────

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'LowProgressTrialAndError',
    'All',
    1,
    'Odin',
    'Slow down you''re swapping symbols fast, but the error hasn''t changed. Read the red message at the bottom. What is it telling you to fix?',
    NULL,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'LowProgressTrialAndError'
      AND tier = 1
);

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'LowProgressTrialAndError',
    'All',
    2,
    'Odin',
    'Switching one number for another rarely fixes a logic error. Try printing out a variable''s value using Console.WriteLine to see what it actually holds — that''s how professional programmers debug.',
    'Example: Console.WriteLine(myArray[i]); — add this inside your loop to see what each value is.',
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'LowProgressTrialAndError'
      AND tier = 2
);

-- ────────────────────────────────────────────────────────────
-- GamingTheSystem
-- Trigger: TimeSinceLastSubmit < 2s OR paste detected OR task < 15s
-- ────────────────────────────────────────────────────────────

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'GamingTheSystem',
    'All',
    1,
    'Odin',
    'I noticed you skipped ahead quickly. That''s okay, but the point here is to learn. Let''s try this one together - what do you think the first step should be?',
    NULL,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'GamingTheSystem'
      AND tier = 1
);

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'GamingTheSystem',
    'All',
    2,
    'Odin',
    'Let''s slow down. Read the problem statement again carefully. What exact value is the problem asking you to print? Make sure your code produces exactly that, not a similar value.',
    'Re-read the problem: what is the expected output? Write it down, then trace your code line by line to see if it produces that exact result.',
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'GamingTheSystem'
      AND tier = 2
);

-- ────────────────────────────────────────────────────────────
-- GenericFallback
-- Last-resort catch-all when no behavioral or category hint matches
-- ────────────────────────────────────────────────────────────

INSERT INTO scaffolding_hints
    (id, diagnostic_category, skill_type, tier, npc_name, dialogue_text, technical_hint, is_active)
SELECT
    gen_random_uuid(),
    'GenericFallback',
    'All',
    1,
    'Odin',
    'Something isn''t quite right. Try reading the error message carefully — it usually tells you exactly which line to look at and what the problem is.',
    NULL,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM scaffolding_hints
    WHERE diagnostic_category = 'GenericFallback'
      AND tier = 1
);
