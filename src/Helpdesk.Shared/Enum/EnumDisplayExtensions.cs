using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Helpdesk.Shared.Enums;

/// <summary>
/// Provides helper methods for retrieving <see cref="DisplayAttribute"/> metadata from enumeration values.
/// </summary>
public static class EnumDisplayExtensions
{
    /// <summary>
    /// Gets the <see cref="DisplayAttribute"/> associated with the specified enum value, if any.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The associated <see cref="DisplayAttribute"/>, or <c>null</c> if none is defined.</returns>
    public static DisplayAttribute? GetDisplay(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>(false);
    }

    /// <summary>
    /// Gets the display name defined by <see cref="DisplayAttribute"/> for the specified enum value.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The display name, or the enum's name if no display name is defined.</returns>
    public static string DisplayName(this Enum value)
        => value.GetDisplay()?.Name ?? value.ToString();

    /// <summary>
    /// Gets the description defined by <see cref="DisplayAttribute"/> for the specified enum value.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The description, or an empty string if none is defined.</returns>
    public static string Description(this Enum value)
        => value.GetDisplay()?.Description ?? string.Empty;

    /// <summary>
    /// Gets the short name defined by <see cref="DisplayAttribute"/> for the specified enum value.
    /// </summary>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The short name, or an empty string if none is defined.</returns>
    public static string ShortName(this Enum value)
        => value.GetDisplay()?.ShortName ?? string.Empty;
}

