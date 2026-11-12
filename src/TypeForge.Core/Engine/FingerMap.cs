namespace TypeForge.Core.Engine;

/// <summary>
/// Identifies the ten touch-typing fingers (with thumbs grouped on the spacebar).
/// </summary>
public enum FingerId
{
    /// <summary>Left pinky finger (home key A).</summary>
    LeftPinky = 0,

    /// <summary>Left ring finger (home key S).</summary>
    LeftRing = 1,

    /// <summary>Left middle finger (home key D).</summary>
    LeftMiddle = 2,

    /// <summary>Left index finger (home key F).</summary>
    LeftIndex = 3,

    /// <summary>Left or right thumb (Spacebar).</summary>
    Thumb = 4,

    /// <summary>Right index finger (home key J).</summary>
    RightIndex = 5,

    /// <summary>Right middle finger (home key K).</summary>
    RightMiddle = 6,

    /// <summary>Right ring finger (home key L).</summary>
    RightRing = 7,

    /// <summary>Right pinky finger (home key ;).</summary>
    RightPinky = 8
}

/// <summary>
/// Deterministic mapping from every standard QWERTY character to its ergonomic touch-typing finger and home-row anchor.
/// </summary>
public static class FingerMap
{
    private static readonly IReadOnlyDictionary<char, FingerId> Map = new Dictionary<char, FingerId>
    {
        // Left Pinky
        ['`'] = FingerId.LeftPinky, ['~'] = FingerId.LeftPinky,
        ['1'] = FingerId.LeftPinky, ['!'] = FingerId.LeftPinky,
        ['Q'] = FingerId.LeftPinky, ['A'] = FingerId.LeftPinky, ['Z'] = FingerId.LeftPinky,

        // Left Ring
        ['2'] = FingerId.LeftRing, ['@'] = FingerId.LeftRing,
        ['W'] = FingerId.LeftRing, ['S'] = FingerId.LeftRing, ['X'] = FingerId.LeftRing,

        // Left Middle
        ['3'] = FingerId.LeftMiddle, ['#'] = FingerId.LeftMiddle,
        ['E'] = FingerId.LeftMiddle, ['D'] = FingerId.LeftMiddle, ['C'] = FingerId.LeftMiddle,

        // Left Index
        ['4'] = FingerId.LeftIndex, ['$'] = FingerId.LeftIndex,
        ['5'] = FingerId.LeftIndex, ['%'] = FingerId.LeftIndex,
        ['R'] = FingerId.LeftIndex, ['T'] = FingerId.LeftIndex,
        ['F'] = FingerId.LeftIndex, ['G'] = FingerId.LeftIndex,
        ['V'] = FingerId.LeftIndex, ['B'] = FingerId.LeftIndex,

        // Thumb
        [' '] = FingerId.Thumb,

        // Right Index
        ['6'] = FingerId.RightIndex, ['^'] = FingerId.RightIndex,
        ['7'] = FingerId.RightIndex, ['&'] = FingerId.RightIndex,
        ['Y'] = FingerId.RightIndex, ['U'] = FingerId.RightIndex,
        ['H'] = FingerId.RightIndex, ['J'] = FingerId.RightIndex,
        ['N'] = FingerId.RightIndex, ['M'] = FingerId.RightIndex,

        // Right Middle
        ['8'] = FingerId.RightMiddle, ['*'] = FingerId.RightMiddle,
        ['I'] = FingerId.RightMiddle, ['K'] = FingerId.RightMiddle,
        [','] = FingerId.RightMiddle, ['<'] = FingerId.RightMiddle,

        // Right Ring
        ['9'] = FingerId.RightRing, ['('] = FingerId.RightRing,
        ['O'] = FingerId.RightRing, ['L'] = FingerId.RightRing,
        ['.'] = FingerId.RightRing, ['>'] = FingerId.RightRing,

        // Right Pinky
        ['0'] = FingerId.RightPinky, [')'] = FingerId.RightPinky,
        ['-'] = FingerId.RightPinky, ['_'] = FingerId.RightPinky,
        ['='] = FingerId.RightPinky, ['+'] = FingerId.RightPinky,
        ['P'] = FingerId.RightPinky,
        ['['] = FingerId.RightPinky, ['{'] = FingerId.RightPinky,
        [']'] = FingerId.RightPinky, ['}'] = FingerId.RightPinky,
        ['\\'] = FingerId.RightPinky, ['|'] = FingerId.RightPinky,
        [';'] = FingerId.RightPinky, [':'] = FingerId.RightPinky,
        ['\''] = FingerId.RightPinky, ['"'] = FingerId.RightPinky,
        ['/'] = FingerId.RightPinky, ['?'] = FingerId.RightPinky
    };

    /// <summary>
    /// All 47 unshifted QWERTY keys (26 letters, 10 digits, 10 punctuation keys, and space).
    /// </summary>
    public static IReadOnlyList<char> AllStandardQwertyKeys { get; } =
    [
        '`', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '-', '=',
        'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', '[', ']', '\\',
        'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', ';', '\'',
        'Z', 'X', 'C', 'V', 'B', 'N', 'M', ',', '.', '/', ' '
    ];

    /// <summary>
    /// Returns the assigned <see cref="FingerId"/> for the given character.
    /// </summary>
    public static FingerId GetFinger(char ch)
    {
        char upper = char.ToUpperInvariant(ch);
        return Map.TryGetValue(upper, out FingerId finger) ? finger : FingerId.Thumb;
    }

    /// <summary>
    /// Attempts to look up the assigned <see cref="FingerId"/> for the given character.
    /// </summary>
    public static bool TryGetFinger(char ch, out FingerId finger)
    {
        char upper = char.ToUpperInvariant(ch);
        return Map.TryGetValue(upper, out finger);
    }

    /// <summary>
    /// Returns a human-readable display name for the specified finger.
    /// </summary>
    public static string GetDisplayName(FingerId finger) => finger switch
    {
        FingerId.LeftPinky => "Left Pinky",
        FingerId.LeftRing => "Left Ring",
        FingerId.LeftMiddle => "Left Middle",
        FingerId.LeftIndex => "Left Index",
        FingerId.Thumb => "Thumb (Spacebar)",
        FingerId.RightIndex => "Right Index",
        FingerId.RightMiddle => "Right Middle",
        FingerId.RightRing => "Right Ring",
        FingerId.RightPinky => "Right Pinky",
        _ => "Thumb (Spacebar)"
    };

    /// <summary>
    /// Returns the home-row anchor key character for the specified finger.
    /// </summary>
    public static char GetHomeRowAnchor(FingerId finger) => finger switch
    {
        FingerId.LeftPinky => 'A',
        FingerId.LeftRing => 'S',
        FingerId.LeftMiddle => 'D',
        FingerId.LeftIndex => 'F',
        FingerId.Thumb => ' ',
        FingerId.RightIndex => 'J',
        FingerId.RightMiddle => 'K',
        FingerId.RightRing => 'L',
        FingerId.RightPinky => ';',
        _ => ' '
    };

    /// <summary>
    /// Builds a concise ergonomic coaching hint for reaching <paramref name="expectedChar"/>.
    /// </summary>
    public static string BuildReachHint(char expectedChar)
    {
        FingerId finger = GetFinger(expectedChar);
        string fingerName = GetDisplayName(finger);
        if (expectedChar == ' ')
        {
            return $"Use {fingerName} for Space";
        }

        char upper = char.ToUpperInvariant(expectedChar);
        char anchor = GetHomeRowAnchor(finger);
        return upper == anchor
            ? $"Use {fingerName} on home key '{upper}'"
            : $"Use {fingerName} (from home key '{anchor}') for '{upper}'";
    }
}
