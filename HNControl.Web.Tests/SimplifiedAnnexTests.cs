using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HNControl.Web.Models;
using HNControl.Web.Services;

internal static class SimplifiedAnnexTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly string[] OptionalRows = ["Subida y simetría acordadas", "Instalación o activación pactada",
        "Título y titular declarados", "Registro del modelo PROFECO"];

    public static void Run(ITemplateDocxService templates, Action<bool, string> check)
    {
        var client = new Client { Name = "CLIENTE DEMO · DOCUMENTO DE PRUEBA", ClientCode = "DEMO-0005",
            LegalRepresentative = "Representante DEMO", Email = "cliente@example.test", BillingEmail = "cliente@example.test",
            Rfc = "DEM010101AA1", State = "Ciudad de México", FiscalZipCode = "01000", FiscalAddress = "Domicilio fiscal DEMO" };
        var provider = new ContractProviderIdentity("PRESTADOR HN DEMO · DATOS DE PRUEBA", "AAA010101AAA",
            "Domicilio completo HN DEMO, Ciudad de México", "01000");
        var contract = new ClientServiceContract { ServiceType = ClientServiceType.Internet, Label = "Internet Dedicado",
            ContractNumber = "DEMO-0005-01", Branch = "Sede DEMO", BranchAddress = "Ubicación del servicio DEMO, Ciudad de México",
            MonthlyAmount = 4500, ContractStartDate = new DateTime(2026, 12, 1), ContractEndDate = new DateTime(2027, 12, 1),
            Notes = "[META] Plazo=12\n[META] InternetCapacidad=100\n[META] CostoInstalacion=1500\n[META] AnexoModalidad=Dedicado",
            PortalUrl = "https://supplier-secret.example.test", PortalUsername = "supplier-secret-user",
            PortalPasswordProtected = "supplier-protected-password" };
        var doc = new ClientLegalDocument { DocumentType = ClientLegalDocumentType.Contract, Status = ClientLegalDocumentStatus.Draft,
            Title = "Contrato DEMO con condiciones simplificadas", CreatedAt = new DateTime(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc),
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider) };
        var snapshot = JsonSerializer.Deserialize<ClientSigningSnapshot>(doc.SigningSnapshotJson)!;
        var minimalBytes = templates.BuildClientLegalDocx(doc, client, contract);
        var minimalXml = Read(minimalBytes);
        var minimalText = Text(minimalXml);
        Directory.CreateDirectory("artifacts/sla-annex-v5");
        File.WriteAllBytes("artifacts/sla-annex-v5/contrato-minimo-pendiente.docx", minimalBytes);
        check(snapshot.ServiceTemplateRevision == 5 && ClientServiceAnnex.MissingRequired(contract).Count == 0,
            "V5 permite contrato completo con modalidad, bajada, ubicación, vigencia y cargos");
        check(minimalText.Contains("Internet Dedicado 100 Mbps") && minimalText.Contains(contract.BranchAddress)
            && minimalText.Contains("12 meses") && minimalText.Contains("4,500.00") && minimalText.Contains("1,500.00"),
            "V5 toma velocidad, sede, vigencia y cargos de los datos existentes");
        check(OptionalRows.All(label => !HasRow(minimalXml, label)) && !minimalText.Contains("No especificad"),
            "V5 omite filas opcionales vacías en lugar de imprimir marcadores pendientes");
        check(!Regex.IsMatch(minimalText, @"\b(?:ANEXO[A-Z]{2,}|HNNOMBRE[A-Z]*|HNRFC|HNDOMICILIO|HNCP)\b"),
            "V5 resuelve todos los tokens de anexo e identidad del prestador");
        check(!doc.SigningSnapshotJson.Contains("supplier-secret") && !doc.SigningSnapshotJson.Contains("supplier-protected-password")
            && !minimalText.Contains("supplier-secret") && !minimalText.Contains("supplier-protected-password"),
            "Ni snapshot ni contrato exponen acceso o contraseña del portal del proveedor");
        contract.Branch = "";
        var noBranchDoc = new ClientLegalDocument { DocumentType = ClientLegalDocumentType.Contract,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider) };
        var noBranchXml = Read(templates.BuildClientLegalDocx(noBranchDoc, client, contract));
        var noBranchService = Text(noBranchXml.Descendants(W + "tr").Single(r =>
            r.Elements(W + "tc").FirstOrDefault() is { } cell && Text(cell).Trim() == "Servicio y sede"));
        check(noBranchService == "Servicio y sede" + contract.Label && !noBranchService.Contains("No especificad"),
            "Sede opcional vacía conserva el nombre del servicio sin un marcador de dato faltante");
        contract.Branch = "Sede DEMO";

        var data = new ClientServiceAnnexData { Modality = "Dedicado", UploadCapacity = "100 Mbps", Symmetry = "Simetrico",
            InstallationDate = new DateTime(2026, 12, 1), AuthorizationReference = "REFERENCIA DEMO SIN VALIDEZ",
            AuthorizationHolder = "TITULAR DEMO", ProfecoRegistration = "REGISTRO DEMO SIN VALIDEZ",
            NetworkMode = "Reventa", PublicIpConditions = "IP-HISTORICA-OCULTA", AccessMedium = "MEDIO-HISTORICO-OCULTO",
            DeliveryPoint = "PUNTO-HISTORICO-OCULTO", Latency = "LATENCIA-HISTORICA-OCULTA", Jitter = "JITTER-HISTORICO-OCULTO",
            PacketLoss = "PERDIDA-HISTORICA-OCULTA", MeasurementScope = "MEDICION-HISTORICA-OCULTA",
            EscalationContact = "RESPONSABLE-HISTORICO-OCULTO" };
        contract.Provider = "OPERADOR-HISTORICO-OCULTO";
        contract.Notes = ClientServiceAnnex.Write(contract.Notes, data);
        doc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider);
        var fullBytes = templates.BuildClientLegalDocx(doc, client, contract);
        var fullXml = Read(fullBytes);
        var fullText = Text(fullXml);
        check(OptionalRows.All(label => HasRow(fullXml, label)) && fullText.Contains("100 Mbps")
            && fullText.Contains("Simétrico") && fullText.Contains("01/12/2026")
            && fullText.Contains(data.AuthorizationReference!) && fullText.Contains(data.ProfecoRegistration!),
            "V5 incluye solo los datos opcionales pactados y las referencias capturadas");
        check(fullXml.Descendants(W + "tr").Count() == minimalXml.Descendants(W + "tr").Count() + OptionalRows.Length,
            "V5 agrega exactamente las cuatro filas opcionales completas");
        check(!fullText.Contains("Reventa") && !fullText.Contains("HISTORIC")
            && !HasRow(fullXml, "Modalidad y red") && !HasRow(fullXml, "Operador de acceso")
            && !HasRow(fullXml, "IP pública y alcance") && !HasRow(fullXml, "Medio y punto de entrega")
            && !HasRow(fullXml, "Parámetros de calidad") && !HasRow(fullXml, "Perímetro de medición"),
            "V5 excluye red, reventa, operador, IP, parámetros técnicos y responsables históricos ocultos");
        check(fullText.Contains(provider.LegalName!) && fullText.Contains(provider.Rfc!) && fullText.Contains(provider.FiscalAddress!),
            "V5 identifica al prestador con los datos fiscales congelados");
        var report = Paragraphs(fullXml).Single(p => p.StartsWith("El usuario puede consultar el monitoreo", StringComparison.Ordinal));
        check(report.Contains("https://support.hubnet-solutions.net") && report.Contains("acceso de cliente")
            && report.Contains("informe mensual dentro de los primeros cinco días hábiles del mes siguiente")
            && report.Contains("solicitar gratuitamente copias o extractos") && report.Contains("recibirlos dentro de cinco días hábiles")
            && report.Contains("mediante el portal, correo o ticket") && !report.Contains("enviará"),
            "V5 ofrece informes en portal y extractos en cinco días sin envío mensual obligatorio por correo");
        check(fullText.Contains("99.85%") && fullText.Contains("Máximo contractual: 4 horas transcurridas, 24/7")
            && fullText.Contains("no es un plazo máximo garantizado"),
            "V5 conserva disponibilidad, respuesta máxima y recuperación como objetivo distinto");
        check(fullText.Contains("Si la activación ocurre después de iniciado el mes")
            && fullText.Contains("únicamente la parte proporcional desde la prestación efectiva hasta el cierre de ese mes"),
            "V5 calcula la primera factura desde la prestación efectiva y no desde la firma");
        check(Paragraphs(fullXml).Single(p => p.StartsWith("DÉCIMA. RESPONSABILIDAD", StringComparison.Ordinal))
            .Contains("Cualquier rescisión por incumplimiento se sujetará a la notificación y procedimiento de la cláusula décima séptima"),
            "V5 sujeta la rescisión al procedimiento pactado en lugar de terminación inmediata automática");
        check(Paragraphs(fullXml).Single(p => p.StartsWith("DÉCIMA SÉPTIMA. RESCISIÓN", StringComparison.Ordinal))
            .Contains("cantidades vencidas, exigibles y no controvertidas, respetando las protecciones y procedimientos de la cláusula décima cuarta"),
            "V5 mantiene la protección de importes controvertidos también en rescisión");
        check(Paragraphs(fullXml).Single(p => p.StartsWith("DECIMA SEGUNDA. CASO", StringComparison.Ordinal))
            .Contains("Subsistirá únicamente el pago de los servicios efectivamente prestados"),
            "V5 limita saldos por fuerza mayor a servicios efectivos y obligaciones legalmente exigibles");
        var currentWelcome = Welcome(client, doc);
        check(currentWelcome.Contains("plataforma de soporte") && currentWelcome.Contains("informe estará disponible")
            && currentWelcome.Contains("primeros cinco días hábiles") && currentWelcome.Contains("solicitar gratuitamente copia o extractos"),
            "Bienvenida v5 explica consulta e informes desde el portal");
        File.WriteAllText("artifacts/sla-annex-v5/bienvenida.html", currentWelcome);

        data.AuthorizationHolder = null;
        data.Symmetry = null;
        contract.Notes = ClientServiceAnnex.Write(contract.Notes, data);
        doc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider);
        var partialXml = Read(templates.BuildClientLegalDocx(doc, client, contract));
        check(!HasRow(partialXml, OptionalRows[0]) && !HasRow(partialXml, OptionalRows[2])
            && HasRow(partialXml, OptionalRows[1]) && HasRow(partialXml, OptionalRows[3]),
            "V5 omite filas de pares incompletos sin ocultar otros opcionales válidos");

        // A prepared v4 retains its full annex and incomplete status even after v5 simplification.
        var legacySnapshot = snapshot with { ServiceTemplateRevision = 4 };
        doc.SigningSnapshotJson = JsonSerializer.Serialize(legacySnapshot);
        var oldPending = Read(templates.BuildClientLegalDocx(doc, client, contract));
        var legacyAnnex = AnnexRows(oldPending);
        check(Text(oldPending).Contains("BORRADOR: faltan datos técnicos") && Text(oldPending).Contains("No especificado")
            && HasRow(oldPending, "Modalidad y red") && HasRow(oldPending, "Operador de acceso"),
            "V4 incompleto conserva sus filas y ANEXOESTADO histórico, sin aplicar mínimos v5");
        var legacyWelcome = Welcome(client, doc);
        check(legacyWelcome.Contains("el punto de entrega, la red utilizada") && !legacyWelcome.Contains("Monitoreo e informes"),
            "Bienvenida v4 conserva su explicación histórica sin incorporar el nuevo portal de informes");
        doc.SignedAt = doc.CreatedAt.AddHours(1);
        doc.SignedByName = "Firma de prueba Cliente DEMO";
        doc.SignedByEmail = "cliente@example.test";
        doc.ClientSignatureImage = File.ReadAllBytes("HNControl.Web.Tests/Fixtures/contract-signature.png");
        check(AnnexRows(Read(templates.BuildClientLegalDocx(doc, client, contract))) == legacyAnnex,
            "Agregar firma a v4 no cambia las filas ni el estado de su anexo congelado");

        data.AuthorizationReference = null;
        data.AuthorizationHolder = null;
        data.ProfecoRegistration = null;
        data.Symmetry = "Simetrico";
        contract.Notes = ClientServiceAnnex.Write(contract.Notes, data);
        doc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, contract, provider: provider);
        doc.ProviderSignedAt = doc.CreatedAt.AddHours(2);
        doc.ProviderSignedByName = "Firma de prueba HN DEMO";
        doc.ProviderSignedByEmail = "hn@example.test";
        doc.ProviderSignedByUserId = "admin-demo";
        doc.ProviderSignatureImage = doc.ClientSignatureImage;
        doc.Status = ClientLegalDocumentStatus.Signed;
        var signedBytes = templates.BuildClientLegalDocx(doc, client, contract);
        var signedText = Text(Read(signedBytes));
        check(!signedText.Contains("REFERENCIA DEMO") && !signedText.Contains("TITULAR DEMO")
            && !signedText.Contains("REGISTRO DEMO"),
            "Vista final DEMO simplificada omite referencias regulatorias no pactadas");
        using (var zip = new ZipArchive(new MemoryStream(signedBytes)))
            check(zip.GetEntry("word/media/autograph-client.png") != null && zip.GetEntry("word/media/autograph-hn.png") != null
                && Regex.Matches(signedText, @"Hash(?: de firma)?:\s*[A-F0-9]{16}").Count == 2,
                "V5 firmado conserva ambas firmas autógrafas y hashes digitales independientes");
        File.WriteAllBytes("artifacts/sla-annex-v5/contrato-demostracion.docx", signedBytes);
        contract.Notes = "[META] InternetCapacidad=999";
        contract.MonthlyAmount = 99999;
        contract.BranchAddress = "Ubicación modificada después";
        client.Name = "CLIENTE MODIFICADO DESPUÉS";
        check(Text(Read(templates.BuildClientLegalDocx(doc, client, contract))) == signedText,
            "Cambios posteriores del cliente y servicio no alteran el contrato v5 firmado");

        var nonInternet = new ClientServiceContract { ServiceType = ClientServiceType.CCTV, Label = "CCTV DEMO", Notes = "[META] Plazo=12" };
        var otherDoc = new ClientLegalDocument { DocumentType = ClientLegalDocumentType.Contract,
            SigningSnapshotJson = ClientSigningSnapshot.Capture(client, nonInternet, provider: provider) };
        var otherXml = Read(templates.BuildClientLegalDocx(otherDoc, client, nonInternet));
        check(!HasRow(otherXml, "Modalidad contratada") && !HasRow(otherXml, "Velocidad contratada de bajada")
            && !HasRow(otherXml, OptionalRows[0]) && Text(otherXml).Contains("servicios distintos de Internet"),
            "Contrato CCTV omite las filas de Internet y limita el alcance del apartado SLA");
        check(!Welcome(client, otherDoc).Contains("99.85%"), "Bienvenida CCTV no promete disponibilidad del servicio Internet");
        nonInternet.ServiceType = ClientServiceType.Internet;
        nonInternet.MonthlyAmount = 1500;
        nonInternet.Notes = "[META] Recurrencia=Eventual";
        nonInternet.ContractStartDate = new DateTime(2026, 12, 1);
        nonInternet.ContractEndDate = new DateTime(2026, 12, 2);
        otherDoc.SigningSnapshotJson = ClientSigningSnapshot.Capture(client, nonInternet, provider: provider);
        check(!Text(Read(templates.BuildClientLegalDocx(otherDoc, client, nonInternet))).Contains("99.85%")
            && !Welcome(client, otherDoc).Contains("99.85%"), "Evento conserva su plantilla y no hereda SLA mensual v5");
    }

    private static string Welcome(Client client, ClientLegalDocument doc) => ClientCommunicationTemplates.Welcome(client,
        new[] { (doc, "https://example.test/firmar") });
    private static bool HasRow(XDocument xml, string label) => xml.Descendants(W + "tr")
        .Any(r => r.Elements(W + "tc").FirstOrDefault() is { } cell && Text(cell).Trim() == label);
    private static string AnnexRows(XDocument xml)
    {
        var rows = xml.Descendants(W + "tr").SkipWhile(r => !Text(r).StartsWith("Servicio y sede", StringComparison.Ordinal))
            .TakeWhile(r => !Text(r).Contains("Firma", StringComparison.Ordinal)).Select(Text);
        var status = Paragraphs(xml).FirstOrDefault(p => p.StartsWith("BORRADOR:", StringComparison.Ordinal)) ?? "";
        return status + string.Join("\n", rows);
    }
    private static string[] Paragraphs(XDocument xml) => xml.Descendants(W + "p").Select(Text).ToArray();
    private static string Text(XContainer xml) => string.Concat(xml.Descendants(W + "t").Select(x => x.Value));
    private static XDocument Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
}
