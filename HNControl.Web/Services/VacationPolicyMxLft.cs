namespace HNControl.Web.Services;

/// <summary>
/// Política de vacaciones (México) basada en la LFT (reforma 2023).
/// Reglas:
/// 1 año: 12, 2: 14, 3: 16, 4: 18, 5: 20,
/// 6-10: 22, 11-15: 24, 16-20: 26, y +2 por cada bloque de 5 años adicional.
/// </summary>
public static class VacationPolicyMxLft
{
    public static int GetAnnualVacationDays(DateTime? hireDate, DateTime? asOf = null)
    {
        if (!hireDate.HasValue) return 0;

        var hd = hireDate.Value.Date;
        var d = (asOf ?? DateTime.Now).Date;

        if (d < hd) return 0;

        // Años COMPLETOS de antigüedad (cumplidos)
        var years = d.Year - hd.Year;
        if (d < hd.AddYears(years)) years--;

        if (years < 1) return 0;

        if (years <= 5)
            return 10 + (years * 2); // 1->12

        // Desde el 6to año: 22 + 2 por cada bloque de 5 años adicional
        var extraBlocks = (years - 6) / 5; // 6-10 =>0, 11-15=>1, 16-20=>2...
        return 22 + (extraBlocks * 2);
    }
}
