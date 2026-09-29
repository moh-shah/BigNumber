# BigNumber
BigNumber — A dependency-free C# struct for idle/incremental games

Clade made me this to be used for my idle/incremental game. Number representation logic follows games like: Cat Snack Bar and Idle Cat Gunner.
Can save you a couple thousand tokens.

------

BigNumber — A dependency-free C# struct for idle/incremental games where numbers outgrow double (past ~1.8e308).
Stores values as mantissa × 10^exponent with a long exponent, so it scales indefinitely.
Formats/parses using a letter-tier suffix (bijective base-26, like Excel columns):
1000 → "1A", 1e6 → "1B", 1e9 → "1C", continuing past "ZZ" into "AAA" and beyond with no special-casing.
Supports full arithmetic (+ - * / Pow) and comparisons.
Ships with an optional Newtonsoft.Json converter for config/save serialization.


------

**How the math works:**

There are two separate pieces here: how the number is stored, and how it's displayed.

1. Storage: scientific notation with a long exponent
A double can't represent idle-game-scale numbers because its own internal exponent only has ~11 bits — it maxes out around 1.8 × 10^308. Past that it just becomes Infinity.

So instead of storing one double, BigNumber stores two numbers:

value = Mantissa × 10^Exponent
Mantissa is a double, always kept in the range [1, 10) (or exactly 0).
Exponent is a long, which can go up to ~9.2 × 10^18 — astronomically more headroom than a double's exponent ever had.
Example: 2500 is stored as Mantissa = 2.5, Exponent = 3 (since 2.5 × 10^3 = 2500). A number like 10^500 is just Mantissa = 1, Exponent = 500 — completely unremarkable to store, even though it'd overflow a double instantly.

Normalize is the function that keeps this invariant true after every operation. If a calculation produces a mantissa like 25.0 or 0.003, Normalize walks it back into [1, 10) by shifting digits into the exponent:

25.0 × 10^3  →  2.5 × 10^4      (shifted right: divide mantissa by 10, exponent+1)
0.003 × 10^3 →  3.0 × 10^0      (shifted left: multiply mantissa by 10, exponent-1)
2. Arithmetic
Multiply / Divide are easy: multiply/divide the mantissas, and just add or subtract the exponents. That's the whole point of scientific notation — (a×10^m) × (b×10^n) = (a×b)×10^(m+n).

Add / Subtract are the tricky ones, because you can't add two numbers in scientific notation unless their exponents match. So the code:

Picks whichever number has the bigger exponent.
Shifts the smaller one's mantissa right by the exponent difference (dividing it by 10^diff) so both are expressed at the same scale.
Adds the mantissas normally, then re-normalizes.
Example: 2.5×10^5 + 3×10^3 → shift the second one: 3×10^3 = 0.03×10^5 → add: (2.5+0.03)×10^5 = 2.53×10^5.

One shortcut: if the exponents differ by more than ~17, the smaller number is thrown away entirely (if (diff > 17) return a;). That's because a double only has about 15–17 significant decimal digits — adding something 10^17 times smaller literally wouldn't change any bit of the result, so there's no point doing the work. This is standard in every idle-game big-number library.

Comparison just compares sign first, then exponent, then mantissa — exactly like comparing two numbers written in scientific notation by eye: whichever has the bigger exponent is bigger, and only if exponents tie do you look at the mantissa.

3. Display: the letter suffix
This is a completely separate step from the math above — it only matters when converting to a string.

Since every 3 decimal digits is a "tier" (10^3 = one tier), the tier number is just:

tier = Exponent / 3   (integer division)
Exponent = 3 → tier 1 → "A". Exponent = 6 → tier 2 → "B". Exponent = 9 → tier 3 → "C".

Then the leftover Exponent % 3 (0, 1, or 2) tells you how much to shift the mantissa within that tier so it displays as a number between 1 and 999 instead of 1 and 10. E.g. 2000 has Exponent = 3, Mantissa = 2; tier = 1 ("A"), remainder = 0, so it displays as 2 + "A" = "2A".

4. Turning a tier number into letters (and back)
This is the part people usually haven't seen before: turning 1 → A, 26 → Z, 27 → AA, 702 → ZZ is bijective base-26 — the exact algorithm Excel uses to name columns.

It's not regular base-26, because regular base-26 would have no digit for "zero" in each position and couldn't distinguish A from AA cleanly. The trick is: before taking the remainder, you subtract 1 first:

ToLetters(n):
    while n > 0:
        n = n - 1                  ← the "bijective" trick
        letter = 'A' + (n % 26)
        prepend letter
        n = n / 26                 (integer division)
Walking through tier = 27:

n=27 → n-1=26 → 26 % 26 = 0 → 'A'. n = 26 / 26 = 1.
n=1 → n-1=0 → 0 % 26 = 0 → 'A'. n = 0. Stop.
Result, read in the order we produced it (prepending each time): "AA".
And tier = 702:

n=702 → 701 % 26 = 25 → 'Z'. n = 701/26 = 26.
n=26 → 25 % 26 = 25 → 'Z'. n = 0. Stop.
Result: "ZZ".
That -1 before the modulo is what makes 26 letters cycle cleanly through unlimited digit-groups without ever needing a "zero" letter — it's why the scheme naturally keeps extending to "AAA" after "ZZ" with the exact same loop, no special-casing required. Parsing (TryFromLetters) just runs that in reverse: tier = tier×26 + (letter - 'A' + 1) for each character, left to right.
