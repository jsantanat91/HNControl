using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HNControl.Web.Services;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString());
        if (member.Length == 0) return value.ToString();

        var attr = member[0].GetCustomAttribute<DisplayAttribute>();
        return string.IsNullOrWhiteSpace(attr?.Name) ? value.ToString() : attr!.Name!;
    }
}
