using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class ClientServiceAnnexTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = new ClientServiceAnnexData { Modality = "Dedicado", NetworkMode = "Reventa",
            UploadCapacity = "100 Mbps", Symmetry = "Simetrico", DeliveryPoint = "Puerto WAN del router del cliente",
            AccessMedium = "Fibra óptica", MeasurementScope = "De NOC a punto de entrega cada 60 segundos",
            InstallationDate = new DateTime(2026, 12, 1),
            PublicIpConditions = "Una IP fija", Latency = "20 ms", Jitter = "5 ms", PacketLoss = "0.1%",
            EscalationContact = "Responsable de operaciones" };
        var notes = ClientServiceAnnex.Write("Observaciones\n[META] Plazo=12\n[META] InternetCapacidad=100\n[META] FactibilidadId=abc", data);
        var roundtrip = ClientServiceAnnex.Read(notes);
        check(roundtrip.Modality == "Dedicado" && roundtrip.Symmetry == "Simetrico"
            && roundtrip.DeliveryPoint == data.DeliveryPoint && notes.Contains("FactibilidadId=abc"),
            "Anexo conserva datos y metadatos ajenos al guardar");
        var contract = new ClientServiceContract { ServiceType = ClientServiceType.Internet,
            Provider = "Proveedor de prueba", Branch = "Sede DEMO", BranchAddress = "Ubicación verificada",
            Notes = notes, Label = "Internet Dedicado", MonthlyAmount = 4500, ContractStartDate = new DateTime(2026, 12, 1) };
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0, "Anexo completo reúne datos técnicos del enlace");
        check(roundtrip.InstallationDate == data.InstallationDate && notes.Contains("AnexoInstalacion=2026-12-01")
            && ClientServiceAnnex.Replacements(contract)["ANEXOINSTALACION"] == "01/12/2026",
            "Fecha pactada de instalación se conserva en ISO y se presenta en formato día mes año");
        contract.Notes = notes.Replace("AnexoInstalacion=2026-12-01", "AnexoInstalacion=2026-02-30");
        check(ClientServiceAnnex.Read(contract.Notes).InstallationDate == null
            && ClientServiceAnnex.MissingRequired(contract).Contains("fecha pactada de instalación o activación válida"),
            "Fecha inválida de instalación no se sustituye por el inicio de vigencia");
        contract.Notes = notes;
        contract.ContractStartDate = null;
        check(ClientServiceAnnex.MissingRequired(contract).Contains("fecha de inicio del servicio"),
            "Anexo no permite emitir sin fecha de inicio del servicio");
        contract.ContractStartDate = new DateTime(2026, 12, 1);
        contract.ContractEndDate = new DateTime(2026, 11, 1);
        check(ClientServiceAnnex.MissingRequired(contract).Contains("fechas de vigencia coherentes"),
            "Anexo bloquea fecha de fin anterior al inicio");
        contract.ContractEndDate = null;
        contract.Notes = notes.Replace("Plazo=12", "Plazo=0");
        check(ClientServiceAnnex.MissingRequired(contract).Contains("plazo o fecha de fin del servicio"),
            "Plazo vacío o no positivo requiere fecha de fin explícita");
        contract.ContractEndDate = new DateTime(2027, 12, 1);
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0,
            "Fechas explícitas válidas permiten vigencia sin plazo en meses");
        contract.ContractEndDate = null;
        contract.Notes = notes.Replace("Plazo=12", "Plazo=Indefinido");
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0,
            "Plazo indefinido explícito no requiere inventar una fecha de fin");
        contract.ContractEndDate = new DateTime(2027, 12, 1);
        check(ClientServiceAnnex.MissingRequired(contract).Contains("plazo indefinido sin fecha de fin"),
            "Plazo indefinido con fecha de fin explícita se rechaza por contradicción");
        contract.Notes = notes;
        var anniversaryAllowed = ClientServiceAnnex.MissingRequired(contract).Count == 0;
        contract.ContractEndDate = new DateTime(2027, 11, 30);
        var eveAllowed = ClientServiceAnnex.MissingRequired(contract).Count == 0;
        contract.ContractEndDate = new DateTime(2028, 12, 1);
        check(anniversaryAllowed && eveAllowed
            && ClientServiceAnnex.MissingRequired(contract).Contains("plazo coherente con las fechas de vigencia"),
            "Plazo de 12 meses admite aniversario o víspera y rechaza duración incompatible");
        contract.ContractStartDate = new DateTime(9999, 12, 1);
        contract.ContractEndDate = null;
        check(ClientServiceAnnex.MissingRequired(contract).Contains("plazo dentro del rango de fechas admitido"),
            "Plazo fuera del rango de fechas se valida sin provocar excepción");
        contract.ContractStartDate = new DateTime(2026, 12, 1);
        contract.Notes = notes.Replace("AnexoMedio=Fibra óptica", "AnexoMedio=")
            .Replace("AnexoMedicion=De NOC a punto de entrega cada 60 segundos", "AnexoMedicion=");
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0
            && ClientServiceAnnex.MissingRequiredV4(contract).Contains("medio de acceso")
            && ClientServiceAnnex.MissingRequiredV4(contract).Contains("perímetro y método de medición"),
            "Contrato simplificado omite requisitos técnicos ocultos sin cambiar validación histórica v4");
        contract.Notes = notes;
        var minimalNotes = "[META] Plazo=12\n[META] InternetCapacidad=100\n[META] AnexoModalidad=Dedicado";
        var minimal = new ClientServiceContract { ServiceType = ClientServiceType.Internet, BranchAddress = "Ubicación DEMO",
            ContractStartDate = new DateTime(2026, 12, 1), Notes = minimalNotes };
        var minimalData = ClientServiceAnnex.Read(minimal.Notes);
        check(ClientServiceAnnex.MissingRequired(minimal).Count == 0 && minimalData.UploadCapacity == null
            && minimalData.Symmetry == null && minimalData.InstallationDate == null,
            "Contrato simplificado admite datos esenciales sin inventar subida, simetría, instalación ni red");
        var legacyMissing = ClientServiceAnnex.MissingRequiredV4(minimal);
        check(new[] { "forma de prestación", "punto de entrega", "medio de acceso", "perímetro y método de medición",
            "velocidad de subida", "simetría", "fecha pactada de instalación o activación" }.All(legacyMissing.Contains),
            "Validación congelada v4 conserva todos sus requisitos anteriores");
        minimal.Notes = minimalNotes + "\n[META] AnexoSubida=100";
        var needsSymmetry = ClientServiceAnnex.MissingRequired(minimal).Contains("simetría para la velocidad de subida indicada");
        minimal.Notes = minimalNotes + "\n[META] AnexoSimetria=Simetrico";
        check(needsSymmetry && ClientServiceAnnex.MissingRequired(minimal).Contains("velocidad de subida para la simetría indicada"),
            "Subida y simetría opcionales se completan juntas cuando se decide indicarlas");
        minimal.Notes = minimalNotes + "\n[META] AnexoSubida=20\n[META] AnexoSimetria=Simetrico";
        check(ClientServiceAnnex.MissingRequired(minimal).Contains("velocidades coherentes con la simetría"),
            "Simetría opcional indicada exige velocidades coherentes");
        var replacement = ClientServiceAnnex.Replacements(contract);
        check(replacement["ANEXOBAJADA"] == "100 Mbps" && replacement["ANEXOSUBIDA"] == "100 Mbps",
            "Anexo usa Mbps sin duplicar unidades");
        check(replacement["ANEXOTITULO"] == "No especificado" && replacement["ANEXOPROFECO"] == "No especificado",
            "Anexo no inventa autorizaciones ni registro PROFECO");
        check(ClientServiceAnnex.MissingRequired(null).Count > 0
            && ClientServiceAnnex.MissingRequired(new ClientServiceContract { ServiceType = ClientServiceType.CCTV }).Count == 0,
            "Anexo requiere vínculo pero no impone parámetros de Internet a CCTV");
        contract.Notes += "\n[META] Recurrencia=Eventual";
        check(ClientServiceAnnex.MissingRequired(contract).Count == 0, "Anexo recurrente no bloquea contratos de eventos");
        var rebuilt = ClientServiceAnnex.PreserveMetadata(notes + "\n[META] InternetCapacidadOtro=viejo\n[META] VentaId=vieja", "Nuevas notas");
        check(rebuilt.Contains("FactibilidadId=abc") && !rebuilt.Contains("InternetCapacidad") && !rebuilt.Contains("VentaId"),
            "Limpiar campos administrados no restaura valores viejos al preservar metadatos desconocidos");
        data.PublicIpConditions = "";
        check(ClientServiceAnnex.Read(ClientServiceAnnex.Write(notes, data)).PublicIpConditions == "Una IP fija",
            "Campo histórico oculto vacío en formulario conserva su metadato anterior");
        var editedNotes = ClientServiceAnnex.Write(ClientServiceAnnex.PreserveMetadata(notes, minimalNotes),
            new ClientServiceAnnexData { Modality = "BestEffort" });
        var preserved = ClientServiceAnnex.Read(editedNotes);
        check(preserved.Modality == "BestEffort" && preserved.NetworkMode == roundtrip.NetworkMode
            && preserved.DeliveryPoint == roundtrip.DeliveryPoint && preserved.AccessMedium == roundtrip.AccessMedium
            && preserved.PublicIpConditions == roundtrip.PublicIpConditions && preserved.Latency == roundtrip.Latency
            && preserved.Jitter == roundtrip.Jitter && preserved.PacketLoss == roundtrip.PacketLoss
            && preserved.MeasurementScope == roundtrip.MeasurementScope && preserved.EscalationContact == roundtrip.EscalationContact
            && editedNotes.Contains("FactibilidadId=abc"),
            "Editar formulario simplificado preserva todos los campos históricos ocultos y referencias desconocidas");
        check(preserved.UploadCapacity == null && preserved.Symmetry == null && preserved.InstallationDate == null
            && !ClientServiceAnnex.Write(notes + "\n[META] AnexoTitulo=referencia anterior", new ClientServiceAnnexData()).Contains("AnexoTitulo="),
            "Campos opcionales visibles pueden limpiarse sin restaurar sus valores anteriores");
        data.EscalationContact = "NOC\n[META] AnexoRed=RedPropia";
        var sanitized = ClientServiceAnnex.Read(ClientServiceAnnex.Write(notes, data));
        check(sanitized.NetworkMode == "Reventa", "Saltos de línea en campos del anexo no inyectan metadatos");
        check(ClientServiceAnnex.ValidateSymmetry(data, "20", null) != null,
            "Enlace simétrico rechaza velocidades de subida y bajada distintas");
        data.UploadCapacity = "1 Gbps";
        check(ClientServiceAnnex.ValidateSymmetry(data, "1000", null) == null,
            "La validación de simetría reconoce equivalencia Gbps y Mbps");
        data.Symmetry = "Asimetrico";
        check(ClientServiceAnnex.ValidateSymmetry(data, "100", null) == null,
            "Enlace asimétrico admite velocidades distintas");
        contract.Notes = ClientServiceAnnex.Write("[META] InternetCapacidad=100", new ClientServiceAnnexData());
        check(ClientServiceAnnex.MissingRequired(contract).Contains("modalidad de Internet"),
            "Registros históricos incompletos exponen faltantes sin asumir dedicado por el nombre");
    }
}
