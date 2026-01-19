using System;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class DateTimeExtensions {
    public static bool IsNewYearsEve(this DateTime date) => date.Month == 12 && date.Day == 31;
    public static bool IsNewYear(this DateTime date) => date.Month == 1 && date.Day == 1;
    public static bool IsNewYearsLike(this DateTime date) => date.IsNewYear() || date.IsNewYearsEve();
    public static bool IsNew(this DateTime date) => date.Month == 1;

    public static bool IsValentinesDay(this DateTime date) => date.Month == 2 && date.Day == 14;
    public static bool IsLoveInTheAir(this DateTime date) => date.Month == 2;

    public static bool IsEaster(this DateTime date) {
        // wtf

        int metonic = date.Year % 19;
        int century = date.Year / 100;
        int year = date.Year % 100;
        int a = century / 4; // leap year stuff
        int b = century % 4;
        int f = (century + 8) / 25; // moon correction
        int g = (century - f + 1) / 3;
        int h = (19 * metonic + century - a - g + 15) % 30; // find the epact
        int i = year / 4; // more leap year stuff
        int k = year % 4;
        int l = (32 + 2 * b + 2 * i - h - k) % 7; // find the day of the week for the Paschal full moon
        int m = (metonic + 11 * h + 22 * l) / 451; // month correction

        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return date.Month == month && date.Day == day;
    }

    public static bool IsAprilFools(this DateTime date) => date.Month == 4 && date.Day == 1;

    public static bool IsIndependenceDay(this DateTime date) => date.Month == 7 && date.Day == 4;

    public static bool IsHalloween(this DateTime date) => date.IsSpooky() && date.Day == 31;
    public static bool IsSpooky(this DateTime date) => date.Month == 10;

    public static bool IsThanksgiving(this DateTime date) {
        if (!date.IsThankful())
            return false;

        // thanksgiving is the fourth Thursday in November
        int thursdayCount = 0;
        for (int day = 1; day <= date.Day; day++) {
            DateTime currentDate = new(date.Year, 11, day);
            if (currentDate.DayOfWeek == DayOfWeek.Thursday) {
                thursdayCount++;
                if (thursdayCount == 4 && currentDate.Day == date.Day)
                    return true;
            }
        }
        return false;
    }
    public static bool IsThankful(this DateTime date) => date.Month == 11;

    public static bool IsXmasEve(this DateTime date) => date.Month == 12 && date.Day == 24;
    public static bool IsXmas(this DateTime date) => date.Month == 12 && date.Day == 25;
    public static bool IsXmasLike(this DateTime date) => date.IsXmas() || date.IsXmasEve();
    public static bool IsHolidaySeason(this DateTime date) => date.Month == 12;
}
