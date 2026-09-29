# BigNumber
BigNumber — A dependency-free C# struct for idle/incremental games

Clade made me this to be used for my idle/incremental game. Number representation logic follows games like: Cat Snack Bar and Idle Cat Gunner.
Can save you a couple thousand tokens.


BigNumber — A dependency-free C# struct for idle/incremental games where numbers outgrow double (past ~1.8e308).
Stores values as mantissa × 10^exponent with a long exponent, so it scales indefinitely.
Formats/parses using a letter-tier suffix (bijective base-26, like Excel columns):
1000 → "1A", 1e6 → "1B", 1e9 → "1C", continuing past "ZZ" into "AAA" and beyond with no special-casing.
Supports full arithmetic (+ - * / Pow) and comparisons.
Ships with an optional Newtonsoft.Json converter for config/save serialization.
