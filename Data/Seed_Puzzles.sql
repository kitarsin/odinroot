-- ODIN Puzzle Seed Data
-- Run once against the Supabase PostgreSQL database.
-- Pre-assigned UUIDs must be set on the matching enemy nodes in the Godot editor.

INSERT INTO puzzles (id, title, description, dungeon_level, order_index, skill_type, starter_code, expected_output, array_concept, is_active) VALUES

-- ── Level 0: The Computer Laboratory (Diagnostic Protocol) ──────────────────
('a0000001-0000-0000-0000-000000000000',
 'The Silent Phantom',
 'I am raising my hand to greet the simulation, but no words come out! My output stream is completely blank! How do I speak to the world?!',
 0, 1, 'ArrayInitialization',
 '// Fix: Make the program output "Hello World"
Console.Write("");',
 'Console.WriteLine("Hello World");',
 NULL, true),

('a0000002-0000-0000-0000-000000000000',
 'The Heavy Allocator',
 'I am trying to smash this identity into the system! I know my ID is a solid, whole number 404, but I don''t have the memory allocated to hold something this heavy! Give me a variable!',
 0, 2, 'ArrayInitialization',
 '// Fix: Declare a variable to hold the system ID 404
// int systemID = ???;',
 'int systemID = 404;',
 NULL, true),

('a0000003-0000-0000-0000-000000000000',
 'The Modulo Slasher',
 'I can slice ten power cycles into three perfect pieces, but my blade cannot calculate what is left over! The remainder is driving me mad! Give me the right operator!',
 0, 3, 'ArrayOperations',
 '// Fix: Get the remainder when 10 is divided by 3
int remainder = 10 / 3;',
 'int remainder = 10 % 3;',
 NULL, true),

('a0000004-0000-0000-0000-000000000000',
 'The Panicking Gatekeeper',
 'Too much data! True? False? Are their access levels high enough?! The gates are open to everyone and my head is splitting! Build me a logic gate to filter them before I crash!',
 0, 4, 'ArrayAccess',
 '// Fix: Allow access only when accessLevel is 5 or higher
int accessLevel = 5;
if (accessLevel < 5)
{
    Console.WriteLine("Access granted");
}',
 'if (accessLevel >= 5)',
 NULL, true),

('a0000005-0000-0000-0000-000000000000',
 'The Memory Mercenary',
 'I need to load three security keys to breach the exit, but my belt only has single-variable pouches! I need a contiguous magazine! Give me a block of memory that holds exactly three!',
 0, 5, 'ArrayInitialization',
 '// Fix: Declare a contiguous block of memory for 3 security keys
int securityKey1 = 0;
int securityKey2 = 0;
int securityKey3 = 0;',
 'int[] securityKeys = new int[3];',
 'ArrayDeclaration', true),

-- ── Level 1: The Library Maze (Single-Dimensional Arrays) ───────────────────
('a0000011-0000-0000-0000-000000000000',
 'The Null-Blade Vanguard',
 'I have the weapons! I have the data! But the armory rack does not exist in the physical realm! I forgot to allocate the memory! Every sword I forge falls into the void!',
 1, 1, 'ArrayInitialization',
 '// Fix: Create an array to hold 5 book IDs
int[] bookIDs; // Missing allocation!',
 'int[] bookIDs = new int[5];',
 'ArrayDeclaration', true),

('a0000012-0000-0000-0000-000000000000',
 'The Out-of-Bounds Evoker',
 'The containment array holds five spells! One, two, three, four, five! So why does my matrix shatter when I try to thrust this flame into slot number five?!',
 1, 2, 'ArrayAccess',
 '// Fix: Place "Manuscript" into the LAST slot of a 5-element shelf
string[] shelf = new string[5];
shelf[5] = "Manuscript"; // Wrong index!',
 'shelf[4] = "Manuscript";',
 'ZeroBasedIndexing', true),

('a0000013-0000-0000-0000-000000000000',
 'The Hardcoded Herald',
 'My torch must process exactly ten embers! It has always been ten! I do not care if the shipment only gave us eight today, my loop will keep burning nothing until it hits ten!',
 1, 3, 'ArrayIteration',
 '// Fix: Iterate through the shipment using its actual length
int[] shipment = {3, 7, 2, 8};
for (int i = 0; i < 10; i++) // Hardcoded 10 - wrong!
{
    Console.WriteLine(shipment[i]);
}',
 'for (int i = 0; i < shipment.Length; i++)',
 'DynamicLength', true),

('a0000014-0000-0000-0000-000000000000',
 'The Type-Clashing Alchemist',
 'Data is data! It does not matter if this shelf is strictly for numerical integer potions, I am forcing this string of alphabetical text into it! Transmute it before my flask explodes!',
 1, 4, 'ArrayOperations',
 '// Fix: Convert the text code to an integer before storing it
int[] deweyCodes = new int[5];
string codeText = "512";
deweyCodes[0] = codeText; // Type mismatch!',
 'deweyCodes[0] = int.Parse(codeText);',
 'TypeConversion', true),

('a0000015-0000-0000-0000-000000000000',
 'The Off-By-One Oracle',
 'I must gather the total power of these floating nodes! But my mind cannot extract them! Help me write a loop to add the value of every single node to my total, or they will scatter forever!',
 1, 5, 'ArrayIteration',
 '// Fix: Sum all the page counts using a foreach loop
int[] pages = {142, 97, 208, 315, 53};
int total = 0;
// Add each page count to total here',
 'foreach (int p in pages) { total += p; }',
 'ArraySum', true),

-- ── Level 2: The Fast Food Maze (Loops & Iteration) ─────────────────────────
('a0000021-0000-0000-0000-000000000000',
 'The Infinite Striker',
 'Punch! Punch! Punch! I am tenderizing order number one, but I forgot how to decrement the queue! My counter is missing! I am trapped in an infinite combo!',
 2, 1, 'ArrayIteration',
 '// Fix: Process all customers and decrement the counter
int customers = 5;
while (customers > 0)
{
    ServeCustomer();
    // Missing: decrement customers!
}',
 'while (customers > 0) { customers--; }',
 NULL, true),

('a0000022-0000-0000-0000-000000000000',
 'The Absolute Severer',
 'Order number four is out of stock?! Then I shall sever the entire batch! My blade knows only to break! Please, teach me how to safely continue to the next order before I destroy the whole shift!',
 2, 2, 'ArrayIteration',
 '// Fix: Skip order 4 but continue processing all others
for (int i = 1; i <= 8; i++)
{
    if (i == 4)
    {
        break; // Wrong! Should skip, not stop
    }
    ProcessOrder(i);
}',
 'if (i == 4) { continue; }',
 NULL, true),

('a0000023-0000-0000-0000-000000000000',
 'The Blind Behemoth',
 'Me cook! Me smash patties on the grill! Wait... is the store even open?! I always execute my actions first and check the boolean rules later! I need a loop that matches my blind rage!',
 2, 3, 'ArrayIteration',
 '// Fix: Execute GrillBurger() at least once, then check the condition
bool isStoreOpen = true;
while (isStoreOpen)
{
    GrillBurger();
}',
 'do { GrillBurger(); } while (isStoreOpen);',
 NULL, true),

('a0000024-0000-0000-0000-000000000000',
 'The Frenzied Slicer',
 'I must slice the potatoes into fries! But where do I start?! What is my limit?! How many per strike?! My for loop parameters are completely shattered! I am slicing the void!',
 2, 4, 'ArrayIteration',
 '// Fix: Slice 5 potatoes at a time until targetAmount is reached
int targetAmount = 50;
for (int f = 0; f <= targetAmount; f++) // Wrong increment!
{
    SlicePotatoes(f);
}',
 'for (int f = 0; f < targetAmount; f += 5)',
 NULL, true),

('a0000025-0000-0000-0000-000000000000',
 'The Nested Beast',
 'Tables! Seats! Rows! Columns! My mind cannot process two dimensions at once! I wiped table one, but the inner loop is trapped in my claws! Build the nested architecture so I can reach the rest!',
 2, 5, 'ArrayIteration',
 '// Fix: Clean every seat at every table using nested loops
int tables = 10;
int seatsPerTable = 4;
for (int t = 0; t < tables; t++)
{
    // Missing inner loop — only cleans seat 0!
    Clean(t, 0);
}',
 'for (int t = 0; t < tables; t++) { for (int s = 0; s < seatsPerTable; s++) { Clean(t, s); } }',
 NULL, true),

-- ── Level 3: The Billiards Hall Maze (Multidimensional Arrays) ──────────────
('a0000031-0000-0000-0000-000000000000',
 'The Matrix Carver',
 'I am thrusting into the void! My strikes need a physical battlefield! I need exactly eight rows and four columns to execute my technique, but I don''t know how to manifest the grid! Manifest the matrix for me!',
 3, 1, 'MultidimensionalArrays',
 '// Fix: Create an 8-row, 4-column integer matrix
int[,] poolTable; // Missing allocation!',
 'int[,] poolTable = new int[8, 4];',
 '2DArrayDeclaration', true),

('a0000032-0000-0000-0000-000000000000',
 'The Coordinate Planter',
 'I must plant my staff at the exact target! Third row! Second column! But my spatial coordinates are completely scrambled! Tell me how to index the exact spot before I lose my balance!',
 3, 2, 'MultidimensionalArrays',
 '// Fix: Set the value at the third row, second column (0-based indexing)
int[,] poolTable = new int[8, 4];
poolTable[3, 2] = 1; // Wrong coordinates!',
 'poolTable[2, 1] = 1;',
 'MatrixIndexing', true),

('a0000033-0000-0000-0000-000000000000',
 'The Span Measurer',
 'I am gauging the horizontal span of our arena! My blade must match the exact width of the grid''s columns! But my system only sees the total volume! Help me extract just the column length!',
 3, 3, 'MultidimensionalArrays',
 '// Fix: Get the number of COLUMNS only, not total elements
int[,] grid = new int[5, 8];
int width = grid.Length; // Wrong! Returns 40 (total cells)',
 'int width = grid.GetLength(1);',
 'DimensionQuery', true),

('a0000034-0000-0000-0000-000000000000',
 'The Null-Fletcher',
 'My arrows are nocked! The nested loops are running! I have the row target! I have the column target! But my fingers are frozen! What is the command to shoot a zero into my current coordinates?!',
 3, 4, 'MultidimensionalArrays',
 '// Fix: Assign 0 to each cell using the loop indices
int[,] poolTable = new int[8, 4];
for (int r = 0; r < poolTable.GetLength(0); r++)
{
    for (int c = 0; c < poolTable.GetLength(1); c++)
    {
        // Missing: assign 0 to the current cell!
    }
}',
 'poolTable[r, c] = 0;',
 'GridPopulation', true),

('a0000035-0000-0000-0000-000000000000',
 'The Out-of-Bounds Slugger',
 'I am swinging with all my might! But the arena keeps changing size! I am using the row limit for the columns, and my club keeps crashing into the void! Build me dynamic loop boundaries before I shatter reality!',
 3, 5, 'MultidimensionalArrays',
 '// Fix: Use GetLength(1) for the column loop, not GetLength(0)
int[,] matrix = new int[6, 9];
for (int row = 0; row < matrix.GetLength(0); row++)
{
    for (int col = 0; col < matrix.GetLength(0); col++) // Bug: uses row limit for cols
    {
        ProcessCell(row, col);
    }
}',
 'for (int col = 0; col < matrix.GetLength(1); col++)',
 'DynamicBoundaries', true);
