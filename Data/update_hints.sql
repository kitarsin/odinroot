-- Add hints column if it doesn't exist
ALTER TABLE puzzles ADD COLUMN IF NOT EXISTS hints JSONB DEFAULT '[]'::JSONB;

-- Update hints for Level 0 (Set to empty)
UPDATE puzzles SET hints = '[]' WHERE dungeon_level = 0;

-- Update hints for Level 1
UPDATE puzzles SET hints = '["To declare an integer array, use the syntax: `int[] arrayName = new int[size];`", "The size of the array should be exactly 5 as requested.", "Remember that array indexing starts at 0.", "To assign a value, use `arrayName[index] = value;`", "Finally, use `Console.WriteLine` to print the value at the correct index."]' WHERE id = 'a0000011-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["The array `shelf` has 5 elements.", "The indices go from 0 to 4.", "To access the last element, use index 4.", "You can also use `shelf.Length - 1` to find the last index dynamically.", "Print the result using `Console.WriteLine()`."]' WHERE id = 'a0000012-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Use a `for` loop to iterate through the array.", "Set the loop condition to `i < shipment.Length`.", "Do not hardcode the length as 4; use `.Length` instead.", "Inside the loop, access the element at index `i`.", "Print each element using `Console.WriteLine(shipment[i]);`"]' WHERE id = 'a0000013-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["To convert a string to an integer, use `int.Parse()` or `Convert.ToInt32()`.", "Store the result in the first slot of the array, which is index 0.", "The array `deweyCodes` is already declared for you.", "Access index 0 using `deweyCodes[0]`.", "Print the stored value using `Console.WriteLine`."]' WHERE id = 'a0000014-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Declare a variable to hold the sum, initialized to 0.", "Use a loop to iterate through all elements of the `pages` array.", "Add each element''s value to your sum variable.", "Make sure your loop covers all indices from 0 to `pages.Length - 1`.", "After the loop, print the total sum."]' WHERE id = 'a0000015-0000-0000-0000-000000000000';

-- Update hints for Level 2
UPDATE puzzles SET hints = '["Use a `while` loop with the condition `customers > 0`.", "Inside the loop, print the current value of `customers`.", "Then, decrement `customers` using `customers--;`.", "Ensure you don''t create an infinite loop!", "The loop will stop when `customers` becomes 0."]' WHERE id = 'a0000021-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Use a `for` loop that starts at 1 and goes up to and including 8.", "Inside the loop, add an `if` statement to check if the loop variable is equal to 4.", "If it is 4, use the `continue;` statement to skip the rest of the loop body.", "Outside the `if` statement, print the current number.", "This will print all numbers except 4."]' WHERE id = 'a0000022-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["A `do-while` loop executes the body at least once.", "The syntax is `do { ... } while (condition);`.", "Inside the loop, print the current order number.", "Then increment the order variable.", "The condition should check if `order <= maxOrders`."]' WHERE id = 'a0000023-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Start your `for` loop with `int i = 0;`.", "Set the condition to `i < targetAmount`.", "Increment the loop variable by 5 in each step: `i += 5`.", "Inside the loop, print the value of `i`.", "The output should be 0, 5, 10, 15."]' WHERE id = 'a0000024-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["You need two loops: an outer loop and an inner loop.", "The outer loop should iterate over the tables (0 to `tables - 1`).", "The inner loop should iterate over the seats (0 to `seatsPerTable - 1`).", "Inside the inner loop, construct the string ''Table X Seat Y'' using the loop variables.", "Print the string for each combination."]' WHERE id = 'a0000025-0000-0000-0000-000000000000';

-- Update hints for Level 3
UPDATE puzzles SET hints = '["To declare a 2D array, use `int[,] poolTable = new int[8, 4];`.", "To find the total number of elements, use the `.Length` property.", "`.Length` returns the total count of elements across all dimensions.", "In this case, it should be 8 * 4 = 32.", "Print the length using `Console.WriteLine`."]' WHERE id = 'a0000031-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Access the element at row 2 and column 1 using `poolTable[2, 1]`.", "Assign the value 7 to that element: `poolTable[2, 1] = 7;`.", "Then print the value at that same position.", "Make sure you use the correct zero-based indices.", "The output should be 7."]' WHERE id = 'a0000032-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["To get the length of a specific dimension, use `GetLength(dimension)`.", "Dimension 0 is rows, and dimension 1 is columns.", "Use `grid.GetLength(1)` to get the number of columns.", "The result should be 8.", "Print the result using `Console.WriteLine`."]' WHERE id = 'a0000033-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["Use `poolTable.GetLength(0)` for the outer loop limit (rows).", "Use `poolTable.GetLength(1)` for the inner loop limit (columns).", "Inside the nested loops, assign 1 to `poolTable[i, j]`.", "After filling the array, use another set of nested loops or a single loop to sum all elements.", "Since every cell is 1 and there are 12 cells, the sum should be 12."]' WHERE id = 'a0000034-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["To get the number of rows, use `matrix.GetLength(0)`.", "To get the number of columns, use `matrix.GetLength(1)`.", "Print the row count first, then the column count on a new line.", "The output for the default matrix should be 6 and 9.", "Do not hardcode 6 and 9; use the `GetLength` method."]' WHERE id = 'a0000035-0000-0000-0000-000000000000';

UPDATE puzzles SET hints = '["A jagged array is an array of arrays: `int[][] grid = new int[3][];`", "You need to allocate each row individually using `new int[size]`.", "Use a loop to iterate through the `sizes` array.", "Inside the loop, do `grid[i] = new int[sizes[i]];`.", "Finally, use a loop to print `grid[i].Length` for each row."]' WHERE id = 'a0000099-0000-0000-0000-000000000000';
