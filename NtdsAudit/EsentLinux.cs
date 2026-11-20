namespace Microsoft.Isam.Esent.Interop
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using CODA.NtdsAudit;

    internal enum JET_DbInfo { PageSize }
    internal enum JET_param { DatabasePageSize, Recovery, CircularLog }
    [Flags] internal enum AttachDatabaseGrbit { ReadOnly = 1 }
    [Flags] internal enum OpenDatabaseGrbit { ReadOnly = 1 }
    [Flags] internal enum OpenTableGrbit { ReadOnly = 1, Sequential = 2 }
    [Flags] internal enum EndSessionGrbit { None = 0 }

    internal readonly struct JET_DBID { internal JET_DBID(string path) => Path = path; public string Path { get; } }
    internal readonly struct JET_INSTANCE { public static JET_INSTANCE Nil => default; }
    internal readonly struct JET_SESID
    {
        internal JET_SESID(string path) => Path = path;
        public string Path { get; }
        public static JET_SESID Nil => default;
    }
    internal readonly struct JET_TABLEID
    {
        internal JET_TABLEID(string table) => Table = table;
        public string Table { get; }
    }

    internal static class LinuxEse
    {
        public static string ActiveDbPath { get; set; } = string.Empty;

        private static readonly Dictionary<string, int> Cursors = new();
        private static readonly Dictionary<string, Dictionary<string, JsonElement>> Current = new();
        private static readonly Dictionary<string, List<Dictionary<string, JsonElement>>> TableCache = new();

        public static void MoveBeforeFirst(JET_SESID sesid, JET_TABLEID table)
        {
            Cursors[$"{sesid.Path}|{table.Table}"] = 0;
        }

        public static bool TryMoveNext(JET_SESID sesid, JET_TABLEID table)
        {
            var key = $"{sesid.Path}|{table.Table}";
            if (!Cursors.TryGetValue(key, out var index))
            {
                MoveBeforeFirst(sesid, table);
                index = 0;
            }

            var rows = ReadRows(sesid.Path, table.Table);
            if (index >= rows.Count) return false;
            Current[key] = rows[index];
            Cursors[key] = index + 1;
            return true;
        }

        public static Dictionary<string, JsonElement> CurrentRow(JET_SESID sesid, JET_TABLEID table) =>
            Current[$"{sesid.Path}|{table.Table}"];

        public static List<string> ReadColumnNames(string path, string table)
        {
            path = string.IsNullOrEmpty(path) ? ActiveDbPath : path;
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("No NTDS database has been opened");

            var script = System.IO.Path.Combine(AppContext.BaseDirectory, "esedb-wrapper.py");
            var start = new System.Diagnostics.ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("columns");
            start.ArgumentList.Add(path);
            start.ArgumentList.Add(table);
            start.Environment.TryGetValue("PYTHONPATH", out var pythonPath);
            start.Environment["PYTHONPATH"] = string.IsNullOrEmpty(pythonPath)
                ? "/tmp/dissect-esedb"
                : $"/tmp/dissect-esedb{System.IO.Path.PathSeparator}{pythonPath}";

            using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Failed to start esedb-wrapper.py");
            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"Linux ESE column wrapper failed with exit code {process.ExitCode}");
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }

        public static List<Dictionary<string, JsonElement>> ReadRows(string path, string table)
        {
            path = string.IsNullOrEmpty(path) ? ActiveDbPath : path;
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("No NTDS database has been opened");

            var cacheKey = $"{path}|{table}";
            if (TableCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var stopwatch = Stopwatch.StartNew();

            var script = System.IO.Path.Combine(AppContext.BaseDirectory, "esedb-wrapper.py");
            var start = new System.Diagnostics.ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("rows");
            start.ArgumentList.Add(path);
            start.ArgumentList.Add(table);
            start.Environment.TryGetValue("PYTHONPATH", out var pythonPath);
            start.Environment["PYTHONPATH"] = string.IsNullOrEmpty(pythonPath)
                ? "/tmp/dissect-esedb"
                : $"/tmp/dissect-esedb{System.IO.Path.PathSeparator}{pythonPath}";

            using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Failed to start esedb-wrapper.py");
            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"Linux ESE wrapper failed with exit code {process.ExitCode}");
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json) ?? [];
            TableCache[cacheKey] = rows;
            stopwatch.Stop();
            if (NtdsAuditor.ShowDebugOutput)
                ConsoleEx.WriteDebug($"LinuxEse cache miss: {table} ({rows.Count} rows) in {stopwatch.Elapsed}");
            return rows;
        }

        public static object? Decode(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null) return null;
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("base64", out var base64)) return base64.GetString();
                if (element.TryGetProperty("datetime", out var date))
                    return DateTime.Parse(date.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (element.TryGetProperty("int", out var integer)) return integer.GetInt64();
                if (element.TryGetProperty("float", out var floating)) return floating.GetDouble();
                if (element.TryGetProperty("bool", out var boolean)) return boolean.GetBoolean();
                if (element.TryGetProperty("string", out var text)) return text.GetString();
                return null;
            }
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                return element.GetBoolean();
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt64(out var integer)) return integer;
                return element.GetDouble();
            }
            if (element.ValueKind == JsonValueKind.String) return element.GetString();
            return null;
        }
    }

    internal static class Api
    {
        public static void JetGetDatabaseFileInfo(string path, out int pageSize, JET_DbInfo info) { pageSize = 4096; }
        public static void JetSetSystemParameter(JET_INSTANCE instance, JET_SESID sesid, JET_param param, int value, string? text) { }
        public static void JetSetSystemParameter(JET_INSTANCE instance, JET_SESID sesid, JET_param param, string? text, string? value) { }
        public static void JetCreateInstance(out JET_INSTANCE instance, string name) { instance = JET_INSTANCE.Nil; }
        public static void JetInit(ref JET_INSTANCE instance) { }
        public static void JetBeginSession(JET_INSTANCE instance, out JET_SESID sesid, string? username, string? password) { sesid = new JET_SESID(LinuxEse.ActiveDbPath); }
        public static void JetAttachDatabase(JET_SESID sesid, string path, AttachDatabaseGrbit grbit) { LinuxEse.ActiveDbPath = path; }
        public static void JetOpenDatabase(JET_SESID sesid, string path, string? filename, out JET_DBID dbid, OpenDatabaseGrbit grbit) { LinuxEse.ActiveDbPath = path; dbid = new JET_DBID(path); }
        public static void JetOpenTable(JET_SESID sesid, JET_DBID dbid, string table, object? ignored1, int ignored2, OpenTableGrbit grbit, out JET_TABLEID tableid) { tableid = new JET_TABLEID(table); }
        public static void JetCloseTable(JET_SESID sesid, JET_TABLEID tableid) { }
        public static void JetEndSession(JET_SESID sesid, EndSessionGrbit grbit) { }
        public static void JetTerm(JET_INSTANCE instance) { }

        public static IDictionary<string, JET_COLUMNID> GetColumnDictionary(JET_SESID sesid, JET_TABLEID table)
        {
            var result = new Dictionary<string, JET_COLUMNID>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in LinuxEse.ReadColumnNames(sesid.Path, table.Table))
            {
                result.TryAdd(name, new JET_COLUMNID(name));
            }
            return result;
        }

        public static void MoveBeforeFirst(JET_SESID sesid, JET_TABLEID table) => LinuxEse.MoveBeforeFirst(sesid, table);
        public static bool TryMoveNext(JET_SESID sesid, JET_TABLEID table) => LinuxEse.TryMoveNext(sesid, table);

        public static void RetrieveColumns(JET_SESID sesid, JET_TABLEID table, params ColumnValue[] values)
        {
            var row = LinuxEse.CurrentRow(sesid, table);
            foreach (var value in values)
            {
                row.TryGetValue(value.Columnid.Name, out var element);
                value.SetValue(element.ValueKind == JsonValueKind.Undefined ? null : LinuxEse.Decode(element));
            }
        }
    }

    internal readonly struct JET_COLUMNID
    {
        internal JET_COLUMNID(string name) => Name = name;
        public string Name { get; }
        public override string ToString() => Name;
    }

    internal enum JET_wrn { Success = 0, ColumnNull = 1 }

    internal abstract class ColumnValue
    {
        public JET_COLUMNID Columnid { get; set; }
        public JET_wrn Error { get; internal set; } = JET_wrn.Success;
        internal abstract void SetValue(object? value);
    }

    internal class BytesColumnValue : ColumnValue
    {
        public byte[]? Value { get; private set; }
        internal override void SetValue(object? value)
        {
            if (value is null) { Value = null; Error = JET_wrn.ColumnNull; }
            else if (value is string base64) { Value = Convert.FromBase64String(base64); Error = JET_wrn.Success; }
            else { Value = null; Error = JET_wrn.ColumnNull; }
        }
    }

    internal class StringColumnValue : ColumnValue
    {
        public string? Value { get; private set; }
        internal override void SetValue(object? value)
        {
            if (value is null) { Value = null; Error = JET_wrn.ColumnNull; }
            else { Value = value.ToString(); Error = JET_wrn.Success; }
        }
        protected virtual void GetValueFromBytes(byte[] value, int startIndex, int count, int err)
        {
            Value = System.Text.Encoding.UTF8.GetString(value, startIndex, count);
        }
    }

    internal class Int32ColumnValue : ColumnValue
    {
        public int? Value { get; private set; }
        internal override void SetValue(object? value)
        {
            if (value is null) { Value = null; Error = JET_wrn.ColumnNull; }
            else { Value = Convert.ToInt32(value, CultureInfo.InvariantCulture); Error = JET_wrn.Success; }
        }
    }

    internal class BoolColumnValue : ColumnValue
    {
        public bool? Value { get; private set; }
        internal override void SetValue(object? value)
        {
            if (value is null) { Value = null; Error = JET_wrn.ColumnNull; }
            else { Value = value is bool boolean ? boolean : Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0; Error = JET_wrn.Success; }
        }
    }

    internal class DateTimeColumnValue : ColumnValue
    {
        public DateTime? Value { get; protected set; }
        internal override void SetValue(object? value)
        {
            if (value is null) { Value = null; Error = JET_wrn.ColumnNull; }
            else if (value is DateTime date) { Value = date; Error = JET_wrn.Success; }
            else { Value = null; Error = JET_wrn.ColumnNull; }
        }
    }

    internal class LdapDateTimeColumnValue : DateTimeColumnValue
    {
        internal override void SetValue(object? value)
        {
            if (value is null)
            {
                Value = null;
                Error = JET_wrn.ColumnNull;
                return;
            }

            if (value is long ticks)
            {
                Value = new DateTime(1601, 1, 1).AddTicks(ticks);
                Error = JET_wrn.Success;
                return;
            }

            if (value is DateTime date)
            {
                Value = date;
                Error = JET_wrn.Success;
                return;
            }

            Value = null;
            Error = JET_wrn.ColumnNull;
        }
    }

    internal class Utf8StringColumnValue : StringColumnValue
    {
    }
}
