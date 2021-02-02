-- FIND
print(string.find("Hello Lua user", "Lua"))
-- MATCH
print(string.match("I have 2 questions for you.", "%d+ %a+"))
print(string.format("%d, %q", string.match("I have 2 questions for you.", "(%d+) (%a+)")))
-- GMATCH
for word in string.gmatch("Hello Lua user", "%a+") do print(word) end
-- GSUB
-- Replacements
print(string.gsub("Hello banana", "banana", "Lua user"))
print(string.gsub("banana", "a", "A", 2)) -- limit substitutions made to 2
print(string.gsub("banana", "(an)", "%1-")) -- capture any occurences of "an" and replace
print(string.gsub("banana", "a(n)", "a(%1)")) -- brackets around n's which follow a's
print(string.gsub("banana", "(a)(n)", "%2%1")) -- reverse any "an"s
string.gsub("Hello Lua user", "(%w+)", print) -- print any words found
print(string.gsub("Hello Lua user", "(%w+)", function(w) return string.len(w) end)) -- replace with lengths
print(string.gsub("banana", "(a)", string.upper)) -- make all "a"s found uppercase
print(string.gsub("banana", "(a)(n)", function(a,b) return b..a end)) -- reverse any "an"s
-- Pattern Captures
string.gsub("The big {brown} fox jumped {over} the lazy {dog}.","{(.-)}", function(a)  print(a) end )
string.gsub("The big {brown} fox jumped {over} the lazy {dog}.","{(.*)}", function(a)  print(a) end )
