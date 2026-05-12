-- ODIN Puzzle Seed Data - Pretest Style
-- Run in Supabase SQL Editor.
--
-- Each puzzle provides data declarations as starter code.
-- Students write the logic to produce specific console output.
-- expected_output is the normalized Console output (no trailing newline).
-- test_cases is a JSONB array of secondary anti-hardcoding substitution tests.
--
-- Regex patterns in test_cases use JSON \\ to represent a single backslash,
-- so \\d+ in JSON becomes the regex \d+ (one or more digits).

-- Step 1: Add test_cases column if not already present
ALTER TABLE puzzles ADD COLUMN IF NOT EXISTS test_cases JSONB;

-- Step 2: Remove all existing puzzles
DELETE FROM puzzles;

-- Step 3: Insert all 21 new pretest-style puzzles
INSERT INTO puzzles (id, title, description, dungeon_level, order_index, skill_type, starter_code, expected_output, array_concept, is_active, test_cases, hints) VALUES

-- ── Level 0: The Computer Laboratory (Diagnostic Protocol) ──────────────────

('a0000001-0000-0000-0000-000000000000',
 'The Silent Phantom',
 'I am the echo of the machine - but no sound comes out! I must transmit the first greeting across the network. Write the output statement that gives me a voice: Hello, ODIN!',
 0, 1, 'ArrayInitialization',
 '// Print the message "Hello, ODIN!" to the console.',
 'Hello, ODIN!',
 NULL, true, NULL, \'["Hint 1", "Hint 2"]\'),

('a0000002-0000-0000-0000-000000000000',
 'The Heavy Allocator',
 'A critical system ID has been issued - four hundred and four! But this data drifts in the void with nowhere to land. Declare an integer variable to anchor it, then print its value.',
 0, 2, 'ArrayInitialization',
 '// Declare an integer variable called systemID with the value 404.
// Then print its value.',
 '404',
 NULL, true,
 '[{"find":"int systemID = \\d+","replace":"int systemID = 200","expectedOutput":"200"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000003-0000-0000-0000-000000000000',
 'The Modulo Slasher',
 'My blade slices the power cycles into groups - but what remains after the split?! The remainder is the key. Use the correct operator on the provided values and print what is left over.',
 0, 3, 'ArrayOperations',
 'int a = 10;
int b = 3;
// Print the remainder when a is divided by b.',
 '1',
 NULL, true,
 '[{"find":"int b = \\d+","replace":"int b = 4","expectedOutput":"2"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000004-0000-0000-0000-000000000000',
 'The Panicking Gatekeeper',
 'The access level has been set - the threshold is five! Write the decision logic: if the access level is 5 or higher, print "Access granted". Otherwise, print "Access denied".',
 0, 4, 'ArrayAccess',
 'int accessLevel = 5;
// Print "Access granted" if accessLevel is 5 or higher.
// Otherwise, print "Access denied".',
 'Access granted',
 NULL, true,
 '[{"find":"accessLevel = \\d+","replace":"accessLevel = 3","expectedOutput":"Access denied"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000005-0000-0000-0000-000000000000',
 'The Memory Mercenary',
 'Three security keys must be loaded into contiguous memory - 101, 202, and 303! Declare the array, assign each value to its correct index, and print every key on its own line.',
 0, 5, 'ArrayInitialization',
 '// Declare an integer array called securityKeys with exactly 3 elements.
// Assign 101 to index 0, 202 to index 1, and 303 to index 2.
// Print each element on its own line.',
 E'101\n202\n303',
 'ArrayDeclaration', true,
 '[{"find":"\\b101\\b","replace":"111","expectedOutput":"111\n202\n303"}]', '["Review the problem carefully.", "Look at previous examples."]')
-- ── Level 1: The Library Maze (Single-Dimensional Arrays) ───────────────────

('a0000011-0000-0000-0000-000000000000',
 'The Null-Blade Vanguard',
 'The archive must be catalogued - a rack of exactly five entries! Declare the array, place the identification code 99 at index 2, then print that value to confirm it was stored.',
 1, 1, 'ArrayInitialization',
 '// Declare an integer array called bookIDs with exactly 5 elements.
// Assign 99 to index 2.
// Print the value at index 2.',
 '99',
 'ArrayDeclaration', true,
 '[{"find":"bookIDs\\[2\\] = \\d+","replace":"bookIDs[2] = 77","expectedOutput":"77"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000012-0000-0000-0000-000000000000',
 'The Out-of-Bounds Evoker',
 'The shelf holds five manuscripts in order. I must retrieve the very last tome! Access the final element of the array using its correct index and print its title.',
 1, 2, 'ArrayAccess',
 'string[] shelf = { "Atlas", "Bestiary", "Codex", "Dossier", "Manuscript" };
// Print the last element of the shelf array.',
 'Manuscript',
 'ZeroBasedIndexing', true, NULL, \'["Hint 1", "Hint 2"]\'),

('a0000013-0000-0000-0000-000000000000',
 'The Hardcoded Herald',
 'The shipment has arrived with its cargo! Every item must be logged - one per line. Do not assume the count; let the array tell you its own length. Print every element in sequence.',
 1, 3, 'ArrayIteration',
 'int[] shipment = { 3, 7, 2, 8 };
// Print each element of the shipment array on its own line.',
 E'3\n7\n2\n8',
 'DynamicLength', true,
 '[{"find":"3, 7, 2, 8","replace":"10, 20, 30","expectedOutput":"10\n20\n30"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000014-0000-0000-0000-000000000000',
 'The Type-Clashing Alchemist',
 'A Dewey classification code arrived as text - "512" - but the catalog rack only accepts integers! Convert the string to its numeric form, load it into the first slot, and print the stored value.',
 1, 4, 'ArrayOperations',
 'int[] deweyCodes = new int[5];
string codeText = "512";
// Convert codeText to an integer and store it at deweyCodes[0].
// Print the value at deweyCodes[0].',
 '512',
 'TypeConversion', true,
 '[{"find":"\"512\"","replace":"\"999\"","expectedOutput":"999"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000015-0000-0000-0000-000000000000',
 'The Off-By-One Oracle',
 'The page counts for every volume in the collection are recorded. Sum them all and print the grand total - every page must be counted, no matter how many entries the array holds.',
 1, 5, 'ArrayIteration',
 'int[] pages = { 142, 97, 208, 315, 53 };
// Compute the sum of all elements in the pages array.
// Print the total.',
 '815',
 'ArraySum', true,
 '[{"find":"142, 97, 208, 315, 53","replace":"100, 200, 300","expectedOutput":"600"}]', '["Review the problem carefully.", "Look at previous examples."]')
-- ── Level 2: The Fast Food Maze (Loops & Iteration) ─────────────────────────

('a0000021-0000-0000-0000-000000000000',
 'The Infinite Striker',
 'Five customers stand in the queue! Use a while loop: print the current customer count, then decrement it. Keep going until the queue is cleared - do not let it run forever!',
 2, 1, 'ArrayIteration',
 'int customers = 5;
// Use a while loop: print customers, then decrement it.
// Repeat until customers reaches 0.',
 E'5\n4\n3\n2\n1',
 NULL, true,
 '[{"find":"customers = \\d+","replace":"customers = 3","expectedOutput":"3\n2\n1"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000022-0000-0000-0000-000000000000',
 'The Absolute Severer',
 'Eight orders are queued for processing - but order four is void! Write a loop from 1 to 8. When the counter hits 4, use continue to skip it. Print all other order numbers.',
 2, 2, 'ArrayIteration',
 '// Use a for loop from 1 to 8 (inclusive).
// Skip the value 4 using continue.
// Print all other numbers.',
 E'1\n2\n3\n5\n6\n7\n8',
 NULL, true, NULL, \'["Hint 1", "Hint 2"]\'),

('a0000023-0000-0000-0000-000000000000',
 'The Blind Behemoth',
 'I execute first and check later - that is the way of this kitchen! Use a do-while loop: print the current order number, then increment it. Continue while the order does not exceed the maximum.',
 2, 3, 'ArrayIteration',
 'int order = 1;
int maxOrders = 3;
// Use a do-while loop: print order, then increment it.
// Continue while order <= maxOrders.',
 E'1\n2\n3',
 NULL, true,
 '[{"find":"maxOrders = \\d+","replace":"maxOrders = 5","expectedOutput":"1\n2\n3\n4\n5"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000024-0000-0000-0000-000000000000',
 'The Frenzied Slicer',
 'Batches of five must be cut until the target is reached! Use a for loop starting at zero, incrementing by 5 each step, stopping before the target amount. Print each batch number as the loop advances.',
 2, 4, 'ArrayIteration',
 'int targetAmount = 20;
// Use a for loop starting at 0, incrementing by 5 each iteration, stopping before targetAmount.
// Print each value of the loop variable.',
 E'0\n5\n10\n15',
 NULL, true,
 '[{"find":"targetAmount = \\d+","replace":"targetAmount = 30","expectedOutput":"0\n5\n10\n15\n20\n25"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000025-0000-0000-0000-000000000000',
 'The Nested Beast',
 'Every table has seats, and every seat must be logged! Write nested loops - outer for tables, inner for seats. For each combination print: Table X Seat Y (using the actual index values of X and Y).',
 2, 5, 'ArrayIteration',
 'int tables = 3;
int seatsPerTable = 2;
// Use nested for loops: outer for tables (0 to tables-1), inner for seats (0 to seatsPerTable-1).
// Print each combination as: "Table X Seat Y" (use the actual values of X and Y).',
 E'Table 0 Seat 0\nTable 0 Seat 1\nTable 1 Seat 0\nTable 1 Seat 1\nTable 2 Seat 0\nTable 2 Seat 1',
 NULL, true,
 '[{"find":"int tables = \\d+","replace":"int tables = 2","expectedOutput":"Table 0 Seat 0\nTable 0 Seat 1\nTable 1 Seat 0\nTable 1 Seat 1"}]', '["Review the problem carefully.", "Look at previous examples."]')
-- ── Level 3: The Billiards Hall Maze (Multidimensional Arrays) ──────────────

('a0000031-0000-0000-0000-000000000000',
 'The Matrix Carver',
 'The battlefield must be forged - an eight-by-four grid of integers! Declare the two-dimensional array and print the total number of elements it can hold.',
 3, 1, 'MultidimensionalArrays',
 '// Declare an 8-row, 4-column integer 2D array called poolTable.
// Print the total number of elements it contains.',
 '32',
 '2DArrayDeclaration', true,
 '[{"find":"new int\\[\\d+, \\d+\\]","replace":"new int[6, 5]","expectedOutput":"30"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000032-0000-0000-0000-000000000000',
 'The Coordinate Planter',
 'My staff must be planted at a precise coordinate - row 2, column 1 (zero-based)! Assign the value 7 to that position in the pool table and print the stored value to confirm the strike landed.',
 3, 2, 'MultidimensionalArrays',
 'int[,] poolTable = new int[8, 4];
// Assign the value 7 to the element at row 2, column 1.
// Print the value at that position.',
 '7',
 'MatrixIndexing', true,
 '[{"find":"\\[2, 1\\] = \\d+","replace":"[2, 1] = 15","expectedOutput":"15"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000033-0000-0000-0000-000000000000',
 'The Span Measurer',
 'The arena has five rows and eight columns - but my instruments must confirm only the horizontal span! Do not use the total element count. Use GetLength to extract just the column dimension and print it.',
 3, 3, 'MultidimensionalArrays',
 'int[,] grid = new int[5, 8];
// Print the number of columns in the grid using GetLength.',
 '8',
 'DimensionQuery', true,
 '[{"find":"new int\\[5, \\d+\\]","replace":"new int[5, 12]","expectedOutput":"12"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000034-0000-0000-0000-000000000000',
 'The Null-Fletcher',
 'Every cell in this four-by-three grid must be filled with a value of 1! Use nested loops with GetLength to reach every coordinate. When every cell is filled, sum the entire matrix and print the total.',
 3, 4, 'MultidimensionalArrays',
 'int[,] poolTable = new int[4, 3];
// Use nested for loops with GetLength(0) and GetLength(1) to assign 1 to every cell.
// Then print the total sum of all elements in the array.',
 '12',
 'GridPopulation', true,
 '[{"find":"new int\\[\\d+, 3\\]","replace":"new int[2, 3]","expectedOutput":"6"}]', '["Review the problem carefully.", "Look at previous examples."]')
('a0000035-0000-0000-0000-000000000000',
 'The Out-of-Bounds Slugger',
 'The matrix has six rows and nine columns - each dimension must be named precisely! Use GetLength to extract the row count from dimension 0 and the column count from dimension 1. Print each on its own line.',
 3, 5, 'MultidimensionalArrays',
 'int[,] matrix = new int[6, 9];
// Print the number of rows using GetLength(0).
// Then print the number of columns using GetLength(1).
// Each on its own line.',
 E'6\n9',
 'DynamicBoundaries', true,
 '[{"find":"new int\\[\\d+, \\d+\\]","replace":"new int[4, 7]","expectedOutput":"4\n7"}]', '["Review the problem carefully.", "Look at previous examples."]')
-- ── Level 3: Final Boss ──────────────────────────────────────────────────────

('a0000099-0000-0000-0000-000000000000',
 'The Corrupted Core',
 'My memory is shattered into jagged fragments! Three rows, each a different size - 3, 5, and 2! Allocate each row of the jagged array using the sizes array as your guide. Then print the length of each row to prove they were forged correctly.',
 3, 6, 'JaggedArrays',
 'int[][] grid = new int[3][];
int[] sizes = { 3, 5, 2 };
// Allocate each row of grid using the size from sizes[i].
// Print the length of each row on its own line.',
 E'3\n5\n2',
 'JaggedDeclaration', true,
 '[{"find":"3, 5, 2","replace":"1, 4, 6","expectedOutput":"1\n4\n6"}]', '["Review the problem carefully.", "Look at previous examples."]');
