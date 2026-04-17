using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services;

public sealed class InventoryExportService
{
    public async Task ExportCsvAsync(
        string path,
        DeviceRecord device,
        IReadOnlyList<InventoryHistoryRecord> history,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("區塊,欄位,值");
        AppendCsvRow(builder, "裝置", "裝置 ID", device.DeviceId);
        AppendCsvRow(builder, "裝置", "裝置名稱", device.DeviceName);
        AppendCsvRow(builder, "裝置", "主機名稱", device.HostName);
        AppendCsvRow(builder, "裝置", "Agent 版本", device.AgentVersion);
        AppendCsvRow(builder, "裝置", "解析度", $"{device.ScreenWidth} x {device.ScreenHeight}");
        AppendCsvRow(builder, "裝置", "是否在線", device.IsOnline ? "在線" : "離線");
        AppendCsvRow(builder, "裝置", "是否核准", device.IsAuthorized ? "已核准" : "待核准");
        AppendCsvRow(builder, "裝置", "最後上線", FormatDateTime(device.LastSeenAt));
        AppendInventoryRows(builder, "目前盤點", device.Inventory);

        builder.AppendLine();
        builder.AppendLine("歷史編號,盤點時間,記錄時間,摘要,CPU,記憶體,磁碟,作業系統,Office,最後更新");
        foreach (var item in history.OrderByDescending(static entry => entry.RecordedAt))
        {
            var inventory = item.Inventory;
            builder.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(item.HistoryId.ToString()),
                EscapeCsv(FormatDateTime(item.CollectedAt)),
                EscapeCsv(FormatDateTime(item.RecordedAt)),
                EscapeCsv(item.ChangeSummary),
                EscapeCsv(inventory.CpuName),
                EscapeCsv(FormatBytes(inventory.InstalledMemoryBytes)),
                EscapeCsv(inventory.StorageSummary),
                EscapeCsv(BuildOsSummary(inventory)),
                EscapeCsv(inventory.OfficeVersion),
                EscapeCsv(BuildLastUpdateSummary(inventory))
            }));
        }

        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
    }

    public Task ExportExcelAsync(
        string path,
        DeviceRecord device,
        IReadOnlyList<InventoryHistoryRecord> history,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var fileStream = File.Create(path);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        WriteZipEntry(archive, "[Content_Types].xml", writer =>
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", "rels");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
            writer.WriteEndElement();
            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", "xml");
            writer.WriteAttributeString("ContentType", "application/xml");
            writer.WriteEndElement();
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/workbook.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            writer.WriteEndElement();
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/worksheets/sheet1.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", "/xl/worksheets/sheet2.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        });

        WriteZipEntry(archive, "_rels/.rels", writer =>
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
            writer.WriteAttributeString("Target", "xl/workbook.xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        });

        WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId1");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", "worksheets/sheet1.xml");
            writer.WriteEndElement();
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", "rId2");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", "worksheets/sheet2.xml");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        });

        WriteZipEntry(archive, "xl/workbook.xml", writer =>
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            writer.WriteStartElement("sheets");
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", "目前盤點");
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId1");
            writer.WriteEndElement();
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", "變更追蹤");
            writer.WriteAttributeString("sheetId", "2");
            writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId2");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        });

        WriteZipEntry(archive, "xl/worksheets/sheet1.xml", writer =>
        {
            WriteWorksheet(writer, BuildCurrentWorksheetRows(device));
        });

        WriteZipEntry(archive, "xl/worksheets/sheet2.xml", writer =>
        {
            WriteWorksheet(writer, BuildHistoryWorksheetRows(history));
        });

        return Task.CompletedTask;
    }

    private static IEnumerable<string[]> BuildCurrentWorksheetRows(DeviceRecord device)
    {
        yield return ["區塊", "欄位", "值"];
        yield return ["裝置", "裝置 ID", device.DeviceId];
        yield return ["裝置", "裝置名稱", device.DeviceName];
        yield return ["裝置", "主機名稱", device.HostName];
        yield return ["裝置", "Agent 版本", device.AgentVersion];
        yield return ["裝置", "解析度", $"{device.ScreenWidth} x {device.ScreenHeight}"];
        yield return ["裝置", "是否在線", device.IsOnline ? "在線" : "離線"];
        yield return ["裝置", "是否核准", device.IsAuthorized ? "已核准" : "待核准"];
        yield return ["裝置", "最後上線", FormatDateTime(device.LastSeenAt)];

        foreach (var row in BuildInventoryRows("目前盤點", device.Inventory))
        {
            yield return [row.Section, row.Label, row.Value];
        }
    }

    private static IEnumerable<string[]> BuildHistoryWorksheetRows(IReadOnlyList<InventoryHistoryRecord> history)
    {
        yield return ["歷史編號", "盤點時間", "記錄時間", "摘要", "CPU", "記憶體", "磁碟", "作業系統", "Office", "最後更新"];
        foreach (var item in history.OrderByDescending(static entry => entry.RecordedAt))
        {
            var inventory = item.Inventory;
            yield return
            [
                item.HistoryId.ToString(),
                FormatDateTime(item.CollectedAt),
                FormatDateTime(item.RecordedAt),
                item.ChangeSummary,
                inventory.CpuName,
                FormatBytes(inventory.InstalledMemoryBytes),
                inventory.StorageSummary,
                BuildOsSummary(inventory),
                inventory.OfficeVersion,
                BuildLastUpdateSummary(inventory)
            ];
        }
    }

    private static void WriteWorksheet(XmlWriter writer, IEnumerable<string[]> rows)
    {
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");

        var rowIndex = 1;
        foreach (var rowValues in rows)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
            for (var columnIndex = 0; columnIndex < rowValues.Length; columnIndex++)
            {
                writer.WriteStartElement("c");
                writer.WriteAttributeString("r", $"{GetColumnName(columnIndex + 1)}{rowIndex}");
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteStartElement("t");
                writer.WriteString(rowValues[columnIndex] ?? string.Empty);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            rowIndex++;
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteZipEntry(ZipArchive archive, string path, Action<XmlWriter> writeContent)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true
        });
        writeContent(writer);
    }

    private static string GetColumnName(int index)
    {
        var builder = new StringBuilder();
        while (index > 0)
        {
            var modulo = (index - 1) % 26;
            builder.Insert(0, (char)('A' + modulo));
            index = (index - modulo) / 26;
        }

        return builder.ToString();
    }

    private static void AppendInventoryRows(StringBuilder builder, string section, AgentInventoryProfile? inventory)
    {
        foreach (var row in BuildInventoryRows(section, inventory))
        {
            AppendCsvRow(builder, row.Section, row.Label, row.Value);
        }
    }

    private static IEnumerable<(string Section, string Label, string Value)> BuildInventoryRows(string section, AgentInventoryProfile? inventory)
    {
        if (inventory is null)
        {
            yield return (section, "狀態", "尚未收到盤點資料");
            yield break;
        }

        yield return (section, "盤點時間", FormatDateTime(inventory.CollectedAt));
        yield return (section, "CPU", inventory.CpuName);
        yield return (section, "總記憶體", FormatBytes(inventory.InstalledMemoryBytes));
        yield return (section, "磁碟摘要", inventory.StorageSummary);
        yield return (section, "作業系統", BuildOsSummary(inventory));
        yield return (section, "Office", inventory.OfficeVersion);
        yield return (section, "最後更新", BuildLastUpdateSummary(inventory));
    }

    private static void AppendCsvRow(StringBuilder builder, string section, string label, string value)
    {
        builder.AppendLine(string.Join(",", new[]
        {
            EscapeCsv(section),
            EscapeCsv(label),
            EscapeCsv(value)
        }));
    }

    private static string EscapeCsv(string? value)
    {
        var normalized = value ?? string.Empty;
        if (!normalized.Contains(',') && !normalized.Contains('"') && !normalized.Contains('\n') && !normalized.Contains('\r'))
        {
            return normalized;
        }

        return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string BuildOsSummary(AgentInventoryProfile inventory)
    {
        var name = string.IsNullOrWhiteSpace(inventory.OsName) ? "未知作業系統" : inventory.OsName;
        var version = string.IsNullOrWhiteSpace(inventory.OsVersion) ? "?" : inventory.OsVersion;
        var build = string.IsNullOrWhiteSpace(inventory.OsBuildNumber) ? "?" : inventory.OsBuildNumber;
        return $"{name} {version} ({build})";
    }

    private static string BuildLastUpdateSummary(AgentInventoryProfile inventory)
    {
        var title = string.IsNullOrWhiteSpace(inventory.LastWindowsUpdateTitle) ? "未知更新" : inventory.LastWindowsUpdateTitle;
        var installedAt = inventory.LastWindowsUpdateInstalledAt.HasValue
            ? FormatDateTime(inventory.LastWindowsUpdateInstalledAt.Value)
            : "未知日期";
        return $"{title} / {installedAt}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "未知";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
