namespace TypeForge.Core.Engine;

/// <summary>
/// Determines whether a Caps Lock warning should be shown for the active typing prompt.
/// </summary>
public static class CapsLockGuard
{
    /// <summary>
    /// Returns true when Caps Lock is active and the prompt requires lowercase letters without any uppercase letters.
    /// </summary>
    public static bool ShouldShowWarning(bool isCapsLockOn, string? targetText)
    {
        if (!isCapsLockOn || string.IsNullOrEmpty(targetText))
        {
            return false;
        }

        bool hasLower = false;
        foreach (char ch in targetText)
        {
            if (char.IsUpper(ch))
            {
                return false;
            }

            if (char.IsLower(ch))
            {
                hasLower = true;
            }
        }

        return hasLower;
    }
}
