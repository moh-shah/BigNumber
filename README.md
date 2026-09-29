# BigNumber
A dependency-free C# struct for idle/incremental games

Claude made me this to be used for my idle/incremental game.
Number representation logic follows games like: Cat Snack Bar and Idle Cat Gunner.
Can save you a couple thousand tokens.

Useful for any system where numbers keep climbing past `1.8e308`.

---

## Why BigNumber?

A normal `double` has a limited exponent range. Once values become too large, it stops being meaningful and becomes `Infinity`.

BigNumber avoids that by storing:

- a mantissa
- a large exponent

In other words, it stores values as:

`value = mantissa × 10^exponent`

where:

- mantissa is a `double` kept in the range `[1, 10)` or exactly `0`
- exponent is a `long`, giving enormous headroom

Example:

- `2500` → mantissa = `2.5`, exponent = `3`
- `10^500` → mantissa = `1`, exponent = `500`

This is effectively scientific notation, but with a much larger exponent range than a standard `double` can provide.

---

## Features

- dependency-free C# struct
- supports large numbers past `double` limits
- arithmetic:
  - addition
  - subtraction
  - multiplication
  - division
  - power
- comparisons
- human-readable suffix formatting
- optional `Newtonsoft.Json` converter for save/config serialization

---

## How the math works

There are two separate pieces to understand:

1. storage
2. display

### 1) Storage: scientific notation with a long exponent

A `double` cannot represent idle-game-scale numbers because its exponent range is too small. Past roughly `1.8 × 10^308`, it eventually becomes `Infinity`.

BigNumber stores values as:

`Mantissa × 10^Exponent`

This keeps the number manageable even when it is astronomically large.

The internal normalization process ensures the mantissa always stays in a valid range:

- `1 <= mantissa < 10`
- or `mantissa == 0`

This is done with normalization rules like:

- `25.0 × 10^3` → `2.5 × 10^4`
- `0.003 × 10^3` → `3.0 × 10^0`

### 2) Arithmetic

#### Multiplication / Division

These are straightforward:

- multiply/divide mantissas
- add/subtract exponents

For example:

`(a × 10^m) × (b × 10^n) = (a × b) × 10^(m+n)`

#### Addition / Subtraction

These are trickier because numbers must share the same exponent before they can be added.

BigNumber does this by:

- selecting the larger exponent
- shifting the smaller mantissa to match scale
- adding the mantissas
- renormalizing afterward

Example:

`2.5 × 10^5 + 3 × 10^3`

becomes:

`2.5 × 10^5 + 0.03 × 10^5`

then:

`(2.5 + 0.03) × 10^5 = 2.53 × 10^5`

A small optimization is used when exponents differ by more than about `17`: the smaller value is effectively ignored because `double` precision only has roughly `15–17` significant decimal digits.

#### Comparison

Comparison is done by checking:

1. sign
2. exponent
3. mantissa

This matches normal scientific notation reasoning:

- larger exponent means larger number
- if exponents are equal, compare mantissas

---

## Display: letter-tier suffixes

This is a separate concern from the math itself.

The system groups values into 3-digit tiers:

- `10^3` → one tier
- `10^6` → two tiers
- `10^9` → three tiers

A value is displayed using a suffix pattern based on its exponent.

For example:

- `1000` → `1A`
- `1e6` → `1B`
- `1e9` → `1C`

The suffix system continues past `ZZ` to `AAA`, `AAB`, and so on.

This is not a standard base-10 suffix system. Instead, it uses a bijective base-26 naming system, similar to how Excel column names work.

---

## How the letter suffixes work

The tier number is derived from the exponent:

`tier = Exponent / 3`

This tells you which letter group you are in.

Examples:

- exponent `3` → tier `1` → `A`
- exponent `6` → tier `2` → `B`
- exponent `9` → tier `3` → `C`

The remainder:

`Exponent % 3`

determines the exact placement inside that tier.

This is what allows values like:

- `1,000` → `1A`
- `1,000,000` → `1B`
- `1,000,000,000` → `1C`

to be displayed cleanly and consistently.

---

## Bijective base-26 (Excel-style lettering)

The letter naming scheme is the same idea used by Excel column labels:

- `1` → `A`
- `26` → `Z`
- `27` → `AA`
- `702` → `ZZ`

This is not a normal base-26 system. It uses a bijective approach so there is no special zero digit, which allows names to continue naturally:

- `A`
- `B`
- ...
- `Z`
- `AA`
- `AB`
- ...
- `ZZ`
- `AAA`
- `AAB`

The core algorithm is:

```csharp
while (n > 0)
{
    n = n - 1;
    letter = 'A' + (n % 26);
    prepend letter;
    n = n / 26;
}
```

This offset by `-1` before modulo is the trick that makes the numbering system work cleanly without needing a "zero" symbol.

---

## Example

```csharp
BigNumber a = 2500;
BigNumber b = 3e6;

BigNumber sum = a + b;
BigNumber product = a * b;
BigNumber quotient = b / a;

Console.WriteLine(sum);      // displays in suffix format
Console.WriteLine(product);
Console.WriteLine(quotient);
```

---

## Notes

BigNumber is designed for the kind of scale common in idle and incremental games:

- values that eventually outgrow `double`
- numbers that need readable shorthand
- saves/configs that should still be easy to serialize

The optional Newtonsoft converter makes it easy to store and restore values in save files.

---

## Summary

BigNumber gives you:

- unbounded-ish large-number storage
- safe arithmetic on game-scale values
- human-readable formatting
- lightweight infrastructure with no external dependencies

If you’re building an idle or incremental game and want a number type that keeps working far beyond the normal `double` limit, BigNumber is a practical fit.
