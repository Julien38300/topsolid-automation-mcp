using System;
using NUnit.Framework;
using TopSolidMcpServer.Utils;

namespace TopSolidMcpServer.Tests
{
    [TestFixture]
    public class FamilyCatalogTests
    {
        [TestCase("add_family_catalog_column", "{\"parameter\":\"Length\"}", "AddCatalogColumn")]
        [TestCase("add_family_catalog_row", "{\"code\":\"A\",\"cells\":{\"Length\":{\"type\":\"real\",\"value\":0.125}}}", "SetCatalogCellRealValue")]
        [TestCase("set_family_catalog_cell", "{\"code\":\"A\",\"column\":\"Length\",\"type\":\"real\",\"value\":0.25}", "SetCatalogCellRealValue")]
        [TestCase("read_family_catalog", "{}", "GetCatalogRow")]
        public void BuildsSelfContainedBody(string recipe, string json, string api)
        {
            var body = FamilyCatalogScripts.Build(recipe, json);
            StringAssert.Contains(api, body);
            StringAssert.DoesNotContain("Newtonsoft", body);
            StringAssert.DoesNotContain("StartModification", body);
            StringAssert.DoesNotContain("Pdm.Save", body);
            StringAssert.DoesNotContain("new ElementId", body);
        }

        [TestCase("{}")]
        [TestCase("{\"code\":\"\",\"column\":\"L\",\"type\":\"real\",\"value\":1}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"real\",\"value\":\"1\"}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"real\",\"value\":NaN}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"integer\",\"value\":2147483648}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"boolean\",\"value\":1}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"smart\",\"value\":1}")]
        [TestCase("{\"code\":\"A\",\"code\":\"B\",\"column\":\"L\",\"type\":\"text\",\"value\":\"x\"}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"text\",\"value\":null}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"code\",\"value\":\" \"}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"integer\",\"value\":1.5}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"real\",\"value\":1e999}")]
        [TestCase("{\"code\":\"A\",\"column\":\"L\",\"type\":\"text\",\"value\":\"x\",\"save\":true}")]
        public void RejectsInvalidInputBeforeExecution(string json)
        {
            Assert.Throws<ArgumentException>(() => FamilyCatalogScripts.Build("set_family_catalog_cell", json));
        }

        [Test]
        public void EscapesStringsAndChecksEveryCellBeforeCreatingRow()
        {
            var body = FamilyCatalogScripts.Build("add_family_catalog_row", "{\"code\":\"A\\\"\\n\\u0000\",\"cells\":{\"L\":{\"type\":\"text\",\"value\":\"\\\\\\\"\\n\"}}}");
            StringAssert.Contains("A\\\"\\n\\u0000", body);
            Assert.That(body.IndexOf("Parameter type mismatch", StringComparison.Ordinal), Is.LessThan(body.IndexOf("AddCatalogNewRow", StringComparison.Ordinal)));
        }

        [TestCase("integer", "-2147483648", "SetCatalogCellIntegerValue")]
        [TestCase("integer", "2147483647", "SetCatalogCellIntegerValue")]
        [TestCase("boolean", "true", "SetCatalogCellBooleanValue")]
        [TestCase("boolean", "false", "SetCatalogCellBooleanValue")]
        [TestCase("text", "\"\"", "SetCatalogCellTextValue")]
        [TestCase("code", "\"B\"", "SetCatalogCellCodeValue")]
        public void SupportsPrimitiveBoundaries(string type, string value, string api)
        {
            var json = "{\"code\":\"A\",\"column\":\"X\",\"type\":\"" + type + "\",\"value\":" + value + "}";
            StringAssert.Contains(api, FamilyCatalogScripts.Build("set_family_catalog_cell", json));
        }

        [TestCase("[]")]
        [TestCase("{\"parameter\":\"X\",\"parameter\":\"Y\"}")]
        [TestCase("{\"parameter\":2}")]
        [TestCase("{\"parameter\":\" \"}")]
        public void RejectsInvalidColumnRequests(string json)
        {
            Assert.Throws<ArgumentException>(() => FamilyCatalogScripts.Build("add_family_catalog_column", json));
        }

        [TestCase("{\"code\":\"A\",\"cells\":[]}")]
        [TestCase("{\"code\":\"A\",\"cells\":{\"L\":3}}")]
        [TestCase("{\"code\":\"A\",\"cells\":{\"L\":{\"type\":\"real\",\"value\":1,\"other\":1}}}")]
        public void RejectsInvalidRowRequests(string json)
        {
            Assert.Throws<ArgumentException>(() => FamilyCatalogScripts.Build("add_family_catalog_row", json));
        }

        [Test]
        public void UsesNativeFamilyParameterDiscoveryAndTextIdentity()
        {
            var add = FamilyCatalogScripts.Build("add_family_catalog_column", "{\"parameter\":\"D <71>\"}");
            StringAssert.Contains("Elements.GetElements(docId)", add);
            StringAssert.Contains("ParameterEntity", add);
            StringAssert.Contains("GetFriendlyName(p)", add);
            StringAssert.DoesNotContain("Parameters.GetParameters", add);
            var row = FamilyCatalogScripts.Build("add_family_catalog_row", "{\"code\":\"A\"}");
            StringAssert.Contains("GetParameterType(columns[0]) != ParameterType.Text", row);
            StringAssert.Contains("SetCatalogCellTextValue(row, codeColumn", row);
            StringAssert.DoesNotContain("SetCatalogCellCodeValue", row);
            StringAssert.Contains("availableParameters", FamilyCatalogScripts.Build("read_family_catalog", "{}"));
        }

        [TestCase("text")]
        [TestCase("code")]
        public void PreservesIsoTimestampStrings(string type)
        {
            const string timestamp = "2026-11-12T10:00:00Z";
            var body = FamilyCatalogScripts.Build("set_family_catalog_cell",
                "{\"code\":\"" + timestamp + "\",\"column\":\"" + timestamp + "\",\"type\":\"" + type + "\",\"value\":\"" + timestamp + "\"}");
            StringAssert.Contains("\"" + timestamp + "\"", body);
            StringAssert.Contains("SetCatalogCell" + (type == "text" ? "Text" : "Code") + "Value", body);
        }
    }
}
