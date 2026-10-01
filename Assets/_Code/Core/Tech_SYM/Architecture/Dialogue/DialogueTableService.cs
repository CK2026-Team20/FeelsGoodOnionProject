using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using Cooked.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cooked.Dialogue
{
    /// <summary>Validates once at the external boundary; never exposes a partial or mutable table.</summary>
    public sealed class DialogueTableService : IDialogueTableService
    {
        private static readonly string[] Fields = { "Dialogue_Code", "Sequence", "Name", "Context", "ContextRevealDuration" };
        private readonly string sourceName;
        private Dictionary<string, IReadOnlyList<DialogueRow>> table;
        public DialogueTableService(string sourceName = "DialogueTable") { this.sourceName = sourceName; }
        public bool IsLoaded => table != null;

        public void LoadJson(string json)
        {
            if (IsLoaded) throw new InvalidOperationException("Dialogue table is immutable after initialization.");
            if (string.IsNullOrWhiteSpace(json)) throw Error("JSON", "Document is empty.");
            try
            {
                JObject root;
                using (var reader = new JsonTextReader(new StringReader(json)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;
                    reader.MaxDepth = 16;
                    root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw Error("JSON", "Trailing content is not allowed.");
                }
                if (root.Properties().Count() != 1 || !(root["Rows"] is JArray rows) || rows.Count == 0)
                    throw Error("Rows", "Expected a non-empty Rows array as the only root field.");
                var groups = new Dictionary<string, SortedDictionary<int, DialogueRow>>(StringComparer.Ordinal);
                for (int index = 0; index < rows.Count; index++)
                {
                    string path = "Rows[" + index + "]";
                    if (!(rows[index] is JObject row)) throw Error(path, "Expected a row object.");
                    if (row.Properties().Count() != Fields.Length || Fields.Any(field => row.Property(field, StringComparison.Ordinal) == null))
                        throw Error(path, "Expected exactly: " + string.Join(", ", Fields));
                    string code = StringField(row, "Dialogue_Code", path);
                    if (code != code.Trim()) throw Error(path + ".Dialogue_Code", "Surrounding identifier whitespace is not allowed.");
                    path += " (" + code + ")";
                    if (row["Sequence"].Type != JTokenType.Integer) throw Error(path + ".Sequence", "Expected positive integer.");
                    if (!int.TryParse(row["Sequence"].ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence) || sequence < 1) throw Error(path + ".Sequence", "Outside positive Int32 range.");
                    string name = StringField(row, "Name", path);
                    string context = StringField(row, "Context", path);
                    JToken time = row["ContextRevealDuration"];
                    if (time.Type != JTokenType.Float && time.Type != JTokenType.Integer)
                        throw Error(path + ".ContextRevealDuration", "Expected numeric seconds.");
                    if (!double.TryParse(time.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) || double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0 || duration > float.MaxValue)
                        throw Error(path + ".ContextRevealDuration", "Expected finite, non-negative seconds.");
                    if (!groups.TryGetValue(code, out var group)) groups.Add(code, group = new SortedDictionary<int, DialogueRow>());
                    if (group.ContainsKey((int)sequence)) throw Error(path + ".Sequence", "Duplicate sequence within code.");
                    group.Add((int)sequence, new DialogueRow(code, (int)sequence, name, context, (float)duration));
                }
                var validated = new Dictionary<string, IReadOnlyList<DialogueRow>>(StringComparer.Ordinal);
                foreach (var group in groups) validated.Add(group.Key, Array.AsReadOnly(group.Value.Values.ToArray()));
                table = validated;
            }
            catch (JsonException exception) { throw Error("JSON", exception.Message, exception); }
            catch (OverflowException exception) { throw Error("Rows numeric field", exception.Message, exception); }
        }

        public bool TryGetRows(string dialogueCode, out IReadOnlyList<DialogueRow> rows)
        {
            rows = null;
            return table != null && dialogueCode != null && table.TryGetValue(dialogueCode, out rows);
        }
        private string StringField(JObject row, string field, string path)
        {
            if (row[field].Type != JTokenType.String || string.IsNullOrWhiteSpace(row[field].Value<string>()))
                throw Error(path + "." + field, "Expected non-empty string.");
            return row[field].Value<string>();
        }
        private FormatException Error(string path, string message, Exception inner = null)
            => new FormatException(sourceName + " / " + path + ": " + message, inner);
    }
}
