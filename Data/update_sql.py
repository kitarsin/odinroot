import re

with open('Seed_Puzzles.sql', 'r', encoding='utf-8') as f:
    text = f.read()

# Replace header
text = text.replace('is_active, test_cases) VALUES', 'is_active, test_cases, hints) VALUES')

# Fix the regex: match "),\\s*\\n" or ");\\s*$" where the preceding text is NULL, ]', or true.
text = re.sub(r'(NULL|\]''|true)\),\s*\n', r'\1, ''["Hint 1", "Hint 2"]''),\n', text)
text = re.sub(r'(NULL|\]''|true)\);\s*$', r'\1, ''["Hint 1", "Hint 2"]'');\n', text)

with open('Seed_Puzzles.sql', 'w', encoding='utf-8') as f:
    f.write(text)
