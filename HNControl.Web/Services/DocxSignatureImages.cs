using System.IO.Compression;
using System.Xml.Linq;
using System.Buffers.Binary;

namespace HNControl.Web.Services;

public static class DocxSignatureImages
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static byte[] Insert(byte[] source, byte[]? client, byte[]? provider)
    {
        if (client == null && provider == null) return source;
        using var buffer = new MemoryStream(); buffer.Write(source);
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, true))
        {
            XDocument Read(string name) { using var stream = zip.GetEntry(name)!.Open(); return XDocument.Load(stream); }
            void Write(string name, XDocument xml) { zip.GetEntry(name)?.Delete(); using var stream = zip.CreateEntry(name).Open(); xml.Save(stream); }
            var document = Read("word/document.xml");
            var rels = Read("word/_rels/document.xml.rels");
            var types = Read("[Content_Types].xml");
            var ns = rels.Root!.Name.Namespace;
            if (!types.Root!.Elements().Any(e => (string?)e.Attribute("Extension") == "png"))
                types.Root.Add(new XElement(types.Root.Name.Namespace + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")));
            void Add(byte[]? bytes, string marker, string alternate, string name, int id)
            {
                if (bytes == null) return;
                var anchor = document.Descendants(W + "p").FirstOrDefault(p => {
                    var text = string.Concat(p.Descendants(W + "t").Select(t => t.Value));
                    return text.Contains(marker) || text.Contains(alternate);
                }) ?? throw new InvalidOperationException("No se encontró el espacio de firma en la plantilla.");
                var cell = anchor.Ancestors(W + "tc").FirstOrDefault();
                var slot = cell?.Elements(W + "p").FirstOrDefault(p => p.Descendants(W + "pBdr").Any() && !p.Descendants(W + "t").Any());
                if (slot == null) { slot = new XElement(W + "p"); anchor.AddBeforeSelf(slot); }
                var props = slot.Element(W + "pPr");
                if (props == null) { props = new XElement(W + "pPr"); slot.AddFirst(props); }
                props.Element(W + "spacing")?.Remove();
                props.Add(new XElement(W + "spacing", new XAttribute(W + "before", "0"), new XAttribute(W + "after", "0")));
                if (bytes.Length < 24) throw new InvalidOperationException("Imagen de firma inválida.");
                var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
                if (width <= 0 || height <= 0 || width > 12000 || height > 12000) throw new InvalidOperationException("Dimensiones de firma inválidas.");
                var scale = Math.Min(2286000d / width, 889000d / height);
                var cx = (long)(width * scale); var cy = (long)(height * scale);
                var rid = "rIdAutograph" + Guid.NewGuid().ToString("N");
                rels.Root.Add(new XElement(ns + "Relationship", new XAttribute("Id", rid), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"), new XAttribute("Target", $"media/{name}.png")));
                using (var stream = zip.CreateEntry($"word/media/{name}.png").Open()) stream.Write(bytes);
                slot.Add(XElement.Parse($"""
                <w:r xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:drawing>
                <wp:inline xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" distT="0" distB="0" distL="0" distR="0">
                <wp:extent cx="{cx}" cy="{cy}"/><wp:docPr id="{id}" name="{name}"/><wp:cNvGraphicFramePr/>
                <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                <pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:nvPicPr><pic:cNvPr id="{id}" name="{name}"/><pic:cNvPicPr/></pic:nvPicPr>
                <pic:blipFill><a:blip xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:embed="{rid}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
                <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic>
                </a:graphicData></a:graphic></wp:inline></w:drawing></w:r>
                """));
            }
            Add(client, "FIRMACLIENTE", "SIGNCLIENTSTATUS", "autograph-client", 80001);
            Add(provider, "FIRMAHN", "SIGNHNSTATUS", "autograph-hn", 80002);
            Write("word/document.xml", document); Write("word/_rels/document.xml.rels", rels); Write("[Content_Types].xml", types);
        }
        return buffer.ToArray();
    }
}
