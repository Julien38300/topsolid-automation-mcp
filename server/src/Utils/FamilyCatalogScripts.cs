using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace TopSolidMcpServer.Utils
{
    /// <summary>Validates recipe JSON on the host and emits standalone C# 5 catalog scripts.</summary>
    public static class FamilyCatalogScripts
    {
        public static bool Handles(string recipe)
        {
            return recipe == "read_family_catalog" || recipe == "add_family_catalog_column" ||
                recipe == "add_family_catalog_row" || recipe == "set_family_catalog_cell";
        }

        public static string Build(string recipe, string value)
        {
            if (!Handles(recipe)) throw new ArgumentException("Unknown catalog recipe.");
            JObject input;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(
                    string.IsNullOrWhiteSpace(value) && recipe == "read_family_catalog" ? "{}" : value ?? "")))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    input = JObject.Load(reader,
                        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new ArgumentException("Expected one JSON object only.");
                }
            }
            catch (JsonException ex) { throw new ArgumentException("Expected a JSON object: " + ex.Message); }
            var script = new StringBuilder();
            bool read = recipe == "read_family_catalog";
            if (read) script.AppendLine("DocumentId docId = TopSolidHost.Documents.EditedDocument;");
            script.AppendLine("if (docId.IsEmpty || !TopSolidHost.Families.IsFamily(docId)) throw new InvalidOperationException(\"The active document must be a family.\");");
            if (!read) script.AppendLine("if (TopSolidHost.Families.IsExplicit(docId) || TopSolidHost.Families.IsDerived(docId)) throw new InvalidOperationException(\"Explicit and derived family editing is unsupported.\");");
            script.AppendLine(ResolveColumn);
            if (read)
            {
                Fields(input);
                script.Append(ReadBody);
            }
            else if (recipe == "add_family_catalog_column")
            {
                Fields(input, "parameter");
                var parameter = Required(input, "parameter");
                // Family driver parameters are not returned by Parameters.GetParameters.
                // Resolve only actual parameter entities owned by the active family document.
                script.AppendLine("var candidates = new System.Collections.Generic.List<ElementId>();");
                script.AppendLine("foreach (var e in TopSolidHost.Elements.GetElements(docId)) if (TopSolidHost.Elements.GetTypeFullName(e).EndsWith(\"ParameterEntity\", StringComparison.Ordinal)) candidates.Add(e);");
                script.AppendLine("ElementId driver = resolve(candidates, " + Literal(parameter) + ");");
                script.AppendLine("var driverType = TopSolidHost.Parameters.GetParameterType(driver);");
                script.AppendLine("if (driverType != ParameterType.Real && driverType != ParameterType.Integer && driverType != ParameterType.Boolean && driverType != ParameterType.Text && driverType != ParameterType.Code) throw new InvalidOperationException(\"Unsupported column parameter type.\");");
                script.AppendLine("if (columns.Contains(driver)) throw new InvalidOperationException(\"Column already exists.\");");
                script.AppendLine("foreach (var existing in columns) if (label(existing) == label(driver)) throw new InvalidOperationException(\"Duplicate column name.\");");
                script.AppendLine("TopSolidHost.Families.AddCatalogColumn(docId, driver);\n__message = \"OK: catalog column added.\";");
            }
            else
            {
                bool add = recipe == "add_family_catalog_row";
                Fields(input, add ? new[] { "code", "cells" } : new[] { "code", "column", "type", "value" });
                var code = Required(input, "code");
                script.AppendLine("var codes = TopSolidHost.Families.GetCodes(docId);");
                script.AppendLine("bool exists = false; foreach (var c in codes) if (c == " + Literal(code) + ") exists = true;");
                script.AppendLine(add ? "if (exists) throw new InvalidOperationException(\"Code already exists.\");" : "if (!exists) throw new InvalidOperationException(\"Unknown row code.\");");
                script.AppendLine(CodeColumn);
                var cells = new List<KeyValuePair<string, JObject>>();
                if (add)
                {
                    var token = input["cells"];
                    if (token != null && !(token is JObject)) throw new ArgumentException("cells must be an object.");
                    foreach (var cell in (JObject)token ?? new JObject())
                    {
                        if (string.IsNullOrWhiteSpace(cell.Key) || !(cell.Value is JObject)) throw new ArgumentException("Each cell must have a column name and typed value object.");
                        Fields((JObject)cell.Value, "type", "value");
                        cells.Add(new KeyValuePair<string, JObject>(cell.Key, (JObject)cell.Value));
                    }
                }
                else cells.Add(new KeyValuePair<string, JObject>(Required(input, "column"), input));
                var writes = new StringBuilder();
                for (int i = 0; i < cells.Count; i++)
                {
                    string type = Required(cells[i].Value, "type");
                    string literal = CellLiteral(type, cells[i].Value["value"]);
                    string suffix = char.ToUpperInvariant(type[0]) + type.Substring(1);
                    script.AppendLine("ElementId col" + i + " = resolve(columns, " + Literal(cells[i].Key) + ");");
                    script.AppendLine("if (col" + i + ".Equals(codeColumn)) throw new InvalidOperationException(\"The Code column is reserved for row identity.\");");
                    script.AppendLine("if (TopSolidHost.Parameters.GetParameterType(col" + i + ") != ParameterType." + suffix + ") throw new InvalidOperationException(\"Parameter type mismatch.\");");
                    writes.AppendLine("TopSolidHost.Families.SetCatalogCell" + suffix + "Value(row, col" + i + ", " + literal + ");");
                }
                script.AppendLine(add ? "ElementId row = TopSolidHost.Families.AddCatalogNewRow(docId);" : "ElementId row = TopSolidHost.Families.GetCatalogRow(docId, " + Literal(code) + ");");
                if (add) script.AppendLine("TopSolidHost.Families.SetCatalogCellTextValue(row, codeColumn, " + Literal(code) + ");");
                script.Append(writes);
                script.AppendLine("__message = \"OK: catalog " + (add ? "row added" : "cell updated") + ".\";");
            }
            return script.ToString();
        }

        private static void Fields(JObject input, params string[] allowed)
        {
            foreach (var p in input.Properties()) if (!allowed.Contains(p.Name)) throw new ArgumentException("Unknown field: " + p.Name);
        }

        private static string Required(JObject input, string name)
        {
            var token = input[name];
            if (token == null || token.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token))
                throw new ArgumentException(name + " must be a nonempty string.");
            return (string)token;
        }

        private static string CellLiteral(string type, JToken value)
        {
            if (value == null) throw new ArgumentException("value is required.");
            if (type == "text" || type == "code")
            {
                if (value.Type != JTokenType.String || (type == "code" && string.IsNullOrWhiteSpace((string)value))) throw new ArgumentException("Expected a string value (nonempty for code).");
                return Literal((string)value);
            }
            if (type == "boolean" && value.Type == JTokenType.Boolean) return (bool)value ? "true" : "false";
            if (type == "integer" && value.Type == JTokenType.Integer)
            {
                int number;
                if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number.ToString(CultureInfo.InvariantCulture);
            }
            if (type == "real" && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer))
            {
                double number;
                if (double.TryParse(value.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number))
                    return number.ToString("R", CultureInfo.InvariantCulture) + "d";
            }
            throw new ArgumentException("Unsupported type or mismatched primitive value: " + type);
        }

        private static string Literal(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '\\') text.Append("\\\\");
                else if (c == '"') text.Append("\\\"");
                else if (c == '\n') text.Append("\\n");
                else if (c == '\r') text.Append("\\r");
                else if (c == '\t') text.Append("\\t");
                else if (char.IsControl(c) || c == '\u2028' || c == '\u2029' || char.IsSurrogate(c)) text.Append("\\u" + ((int)c).ToString("x4"));
                else text.Append(c);
            }
            return text.Append('"').ToString();
        }

        private const string ResolveColumn = @"
var columns = TopSolidHost.Families.GetCatalogColumnParameters(docId);
Func<ElementId, string> label = p => {
    string name = TopSolidHost.Elements.GetName(p);
    return string.IsNullOrEmpty(name) ? TopSolidHost.Elements.GetFriendlyName(p) : name;
};
Func<System.Collections.Generic.List<ElementId>, string, ElementId> resolve = (items, name) => {
    ElementId found = ElementId.Empty; int matches = 0;
    foreach (var p in items) if (label(p) == name) { found = p; matches++; }
    if (matches != 1) throw new InvalidOperationException(""Expected exactly one parameter with exact selector: "" + name);
    return found;
};";

        private const string CodeColumn = @"
if (columns.Count == 0 || TopSolidHost.Parameters.GetParameterType(columns[0]) != ParameterType.Text)
    throw new InvalidOperationException(""Expected a Text identity column at catalog index zero."");
ElementId codeColumn = columns[0];";

        private const string ReadBody = @"
Func<string, string> quote = s => {
    var q = new System.Text.StringBuilder().Append((char)34);
    foreach (char c in s ?? """") {
        if (c == '\\' || c == (char)34) q.Append('\\').Append(c);
        else if (char.IsControl(c) || char.IsSurrogate(c)) q.Append(""\\u"").Append(((int)c).ToString(""x4""));
        else q.Append(c);
    }
    return q.Append((char)34).ToString();
};
var result = new System.Text.StringBuilder(""{"").Append(quote(""columns"")).Append("": ["");
bool first = true;
foreach (var col in columns) {
    if (!first) result.Append(','); first = false;
    var type = TopSolidHost.Parameters.GetParameterType(col);
    result.Append('{').Append(quote(""name"")).Append(':').Append(quote(label(col))).Append(',').Append(quote(""type"")).Append(':').Append(quote(type.ToString()));
    if (type == ParameterType.Real) { UnitType unit; string symbol; TopSolidHost.Parameters.GetRealUnit(col, out unit, out symbol); result.Append(',').Append(quote(""unitType"")).Append(':').Append(quote(unit.ToString())).Append(',').Append(quote(""unitSymbol"")).Append(':').Append(quote(symbol)).Append(',').Append(quote(""valueUnit"")).Append(':').Append(quote(""SI"")); }
    result.Append('}');
}
result.Append(""],"").Append(quote(""availableParameters"")).Append("": [""); first = true;
foreach (var p in TopSolidHost.Elements.GetElements(docId)) {
    if (!TopSolidHost.Elements.GetTypeFullName(p).EndsWith(""ParameterEntity"", StringComparison.Ordinal)) continue;
    var type = TopSolidHost.Parameters.GetParameterType(p);
    if (type != ParameterType.Real && type != ParameterType.Integer && type != ParameterType.Boolean && type != ParameterType.Text && type != ParameterType.Code) continue;
    if (!first) result.Append(','); first = false;
    result.Append('{').Append(quote(""name"")).Append(':').Append(quote(label(p))).Append(',').Append(quote(""type"")).Append(':').Append(quote(type.ToString())).Append('}');
}
result.Append(""],"").Append(quote(""rows"")).Append("": [""); first = true;
foreach (var code in TopSolidHost.Families.GetCodes(docId)) {
    if (!first) result.Append(','); first = false;
    var row = TopSolidHost.Families.GetCatalogRow(docId, code);
    result.Append('{').Append(quote(""code"")).Append(':').Append(quote(code)).Append(',').Append(quote(""cells"")).Append("": [""); bool firstCell = true;
    foreach (var col in columns) {
        if (!firstCell) result.Append(','); firstCell = false;
        var type = TopSolidHost.Parameters.GetParameterType(col);
        result.Append('{').Append(quote(""column"")).Append(':').Append(quote(label(col))).Append(',').Append(quote(""value"")).Append(':');
        if (type == ParameterType.Real) { double n = TopSolidHost.Families.GetCatalogCellRealValue(row, col); if (double.IsNaN(n) || double.IsInfinity(n)) throw new InvalidOperationException(""Nonfinite real catalog value.""); result.Append(n.ToString(""R"", System.Globalization.CultureInfo.InvariantCulture)); }
        else if (type == ParameterType.Integer) result.Append(TopSolidHost.Families.GetCatalogCellIntegerValue(row, col).ToString(System.Globalization.CultureInfo.InvariantCulture));
        else if (type == ParameterType.Boolean) result.Append(TopSolidHost.Families.GetCatalogCellBooleanValue(row, col) ? ""true"" : ""false"");
        else if (type == ParameterType.Text) result.Append(quote(TopSolidHost.Families.GetCatalogCellTextValue(row, col)));
        else if (type == ParameterType.Code) result.Append(quote(TopSolidHost.Families.GetCatalogCellCodeValue(row, col)));
        else result.Append(""null,"").Append(quote(""unsupported"")).Append("":true"");
        result.Append('}');
    }
    result.Append(""]}"");
}
return result.Append(""]}"").ToString();";
    }
}
