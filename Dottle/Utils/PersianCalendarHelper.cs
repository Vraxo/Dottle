using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dottle.Utils;

public static partial class PersianCalendarHelper
{
    private static readonly PersianCalendar pc = new();
    private static readonly Regex DateFormatRegex = DateRegex();

    public static string GetCurrentPersianDateString()
    {
        var now = DateTime.Now;
        return GetPersianDateString(now);
    }

    public static string GetPersianDateString(DateTime dt)
    {
        int year = pc.GetYear(dt);
        int month = pc.GetMonth(dt);
        int day = pc.GetDayOfMonth(dt);
        return $"{year:D4}-{month:D2}-{day:D2}";
    }

    public static int GetPersianYear(DateTime dt)
    {
        return pc.GetYear(dt);
    }

    public static int GetPersianMonth(DateTime dt)
    {
        return pc.GetMonth(dt);
    }

    private static readonly string[] PersianMonthNames =
    [
        "", // Index 0 is unused
        "Farvardin", "Ordibehesht", "Khordad",
        "Tir", "Mordad", "Shahrivar",
        "Mehr", "Aban", "Azar",
        "Dey", "Bahman", "Esfand"
    ];

    public static string GetPersianMonthName(int month)
    {
        if (month >= 1 && month <= 12)
        {
            return PersianMonthNames[month];
        }
        return "Invalid Month"; // Or throw an exception
    }

    public static bool TryParsePersianDateString(string dateString, out DateTime result)
    {
        result = DateTime.MinValue;
        var match = DateFormatRegex.Match(dateString);

        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["year"].Value, out int year)) return false;
        if (!int.TryParse(match.Groups["month"].Value, out int month)) return false;
        if (!int.TryParse(match.Groups["day"].Value, out int day)) return false;

        try
        {
            // Validate month and day ranges for the given Persian year
            if (month < 1 || month > 12) return false;
            int daysInMonth = pc.GetDaysInMonth(year, month);
            if (day < 1 || day > daysInMonth) return false;

            result = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid date components for Persian calendar
            return false;
        }
    }

    // Regex is generated at compile time in .NET 7+
    [GeneratedRegex(@"^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$")]
    private static partial Regex DateRegex();
}
