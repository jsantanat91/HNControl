namespace HNControl.Web.Models;

public static class MexicoGeoCatalog
{
    public static IReadOnlyList<string> States { get; } =
    [
        "Aguascalientes",
        "Baja California",
        "Baja California Sur",
        "Campeche",
        "Chiapas",
        "Chihuahua",
        "Ciudad de Mexico",
        "Coahuila",
        "Colima",
        "Durango",
        "Estado de Mexico",
        "Guanajuato",
        "Guerrero",
        "Hidalgo",
        "Jalisco",
        "Michoacan",
        "Morelos",
        "Nayarit",
        "Nuevo Leon",
        "Oaxaca",
        "Puebla",
        "Queretaro",
        "Quintana Roo",
        "San Luis Potosi",
        "Sinaloa",
        "Sonora",
        "Tabasco",
        "Tamaulipas",
        "Tlaxcala",
        "Veracruz",
        "Yucatan",
        "Zacatecas"
    ];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> MunicipalitiesByState { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ciudad de Mexico"] =
            [
                "Alvaro Obregon",
                "Azcapotzalco",
                "Benito Juarez",
                "Coyoacan",
                "Cuajimalpa de Morelos",
                "Cuauhtemoc",
                "Gustavo A. Madero",
                "Iztacalco",
                "Iztapalapa",
                "La Magdalena Contreras",
                "Miguel Hidalgo",
                "Milpa Alta",
                "Tlahuac",
                "Tlalpan",
                "Venustiano Carranza",
                "Xochimilco"
            ],
            ["Estado de Mexico"] =
            [
                "Atizapan de Zaragoza",
                "Chalco",
                "Chimalhuacan",
                "Coacalco de Berriozabal",
                "Cuautitlan",
                "Cuautitlan Izcalli",
                "Ecatepec de Morelos",
                "Huixquilucan",
                "Ixtapaluca",
                "Lerma",
                "Metepec",
                "Naucalpan de Juarez",
                "Nezahualcoyotl",
                "Nicolas Romero",
                "Tecamac",
                "Tenancingo",
                "Texcoco",
                "Tezoyuca",
                "Tlalnepantla de Baz",
                "Toluca",
                "Tultitlan",
                "Valle de Bravo",
                "Valle de Chalco Solidaridad",
                "Zinacantepec"
            ]
        };
}
