using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// What the comparison tool reads of one revision of a TopSolid document: material, parameters,
    /// document properties, operations (order, tree, state) and a geometric summary per shape.
    /// Built by <see cref="DocumentScripts.Snapshot"/> inside TopSolid, parsed here without TopSolid.
    /// </summary>
    /// <remarks>
    /// Line format, tab separated, fields escaped with <see cref="Escape"/>:
    /// <code>
    /// SNAP   document  revision
    /// REVS   A.0,B.0
    /// MAT    material name
    /// PROP   full name  display value
    /// PARAM  id  name  friendly name  kind  raw value  unit type  unit symbol
    /// OP     index  id  type  friendly name  owner id  active  frozen  invalid  child ids
    /// SHAPE  name  face count  truncated
    /// SURF   shape  surface type  count
    /// CYL    shape  diameter mm  length mm  count
    /// ERR    message
    /// </code>
    /// </remarks>
    public sealed class DocumentSnapshot
    {
        public string Document = "";
        public string Revision = "";
        public List<string> Revisions = new List<string>();
        public string Material = "";
        public List<KeyValuePair<string, string>> Properties = new List<KeyValuePair<string, string>>();
        public List<SnapshotParameter> Parameters = new List<SnapshotParameter>();
        public List<SnapshotOperation> Operations = new List<SnapshotOperation>();
        public List<SnapshotShape> Shapes = new List<SnapshotShape>();
        public List<string> Errors = new List<string>();

        /// <summary>Document and revision, e.g. "Pièce A B.0".</summary>
        public string Label
        {
            get { return string.IsNullOrEmpty(Revision) ? Document : Document + " " + Revision; }
        }

        /// <summary>True when the parameter is created and driven by an operation (table, formula, smart parameter).</summary>
        public bool IsDriven(SnapshotParameter parameter)
        {
            foreach (var op in Operations)
                if (op.Children.Contains(parameter.Id)) return true;
            return false;
        }

        /// <summary>The operation that owns a driven parameter, or null.</summary>
        public SnapshotOperation DrivingOperation(SnapshotParameter parameter)
        {
            foreach (var op in Operations)
                if (op.Children.Contains(parameter.Id)) return op;
            return null;
        }

        public SnapshotParameter FindParameter(string name)
        {
            foreach (var p in Parameters)
                if (p.Name == name) return p;
            return null;
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");
        }

        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s ?? "";
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Parses the output of the snapshot script. Unknown lines are ignored.</summary>
        public static DocumentSnapshot Parse(string text)
        {
            var snap = new DocumentSnapshot();
            var shapes = new Dictionary<string, SnapshotShape>();
            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0) continue;
                string[] f = raw.Split('\t').Select(Unescape).ToArray();
                switch (f[0])
                {
                    case "SNAP":
                        snap.Document = Field(f, 1);
                        snap.Revision = Field(f, 2);
                        break;
                    case "REVS":
                        snap.Revisions = Field(f, 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        break;
                    case "MAT":
                        snap.Material = Field(f, 1);
                        break;
                    case "PROP":
                        snap.Properties.Add(new KeyValuePair<string, string>(Field(f, 1), Field(f, 2)));
                        break;
                    case "PARAM":
                        snap.Parameters.Add(new SnapshotParameter
                        {
                            Id = Int(Field(f, 1)),
                            Name = Field(f, 2),
                            FriendlyName = Field(f, 3),
                            Kind = Field(f, 4),
                            Value = Field(f, 5),
                            UnitType = Field(f, 6),
                            UnitSymbol = Field(f, 7)
                        });
                        break;
                    case "OP":
                        snap.Operations.Add(new SnapshotOperation
                        {
                            Index = Int(Field(f, 1)),
                            Id = Int(Field(f, 2)),
                            Type = Field(f, 3),
                            Name = Field(f, 4),
                            OwnerId = Int(Field(f, 5)),
                            Active = Field(f, 6) == "1",
                            Frozen = Field(f, 7) == "1",
                            Invalid = Field(f, 8) == "1",
                            Children = Field(f, 9).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(Int).ToList()
                        });
                        break;
                    case "SHAPE":
                        var shape = new SnapshotShape { Name = Field(f, 1), FaceCount = Int(Field(f, 2)), Truncated = Field(f, 3) == "1" };
                        shapes[shape.Name] = shape;
                        snap.Shapes.Add(shape);
                        break;
                    case "SURF":
                        SnapshotShape s1;
                        if (shapes.TryGetValue(Field(f, 1), out s1)) s1.Surfaces[Field(f, 2)] = Int(Field(f, 3));
                        break;
                    case "CYL":
                        SnapshotShape s2;
                        if (shapes.TryGetValue(Field(f, 1), out s2))
                            s2.Cylinders.Add(new CylinderGroup { DiameterMm = Dbl(Field(f, 2)), LengthMm = Dbl(Field(f, 3)), Count = Int(Field(f, 4)) });
                        break;
                    case "ERR":
                        snap.Errors.Add(Field(f, 1));
                        break;
                }
            }
            return snap;
        }

        private static string Field(string[] f, int i)
        {
            return i < f.Length ? f[i] : "";
        }

        private static int Int(string s)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : -1;
        }

        internal static double Dbl(string s)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }
    }

    public sealed class SnapshotParameter
    {
        public int Id;
        public string Name = "";
        public string FriendlyName = "";
        /// <summary>Real, Text, Integer, Boolean, Enumeration...</summary>
        public string Kind = "";
        /// <summary>Raw value: SI units for a Real, invariant culture.</summary>
        public string Value = "";
        /// <summary>TopSolid UnitType of a Real (Length, Angle, Mass...).</summary>
        public string UnitType = "";
        public string UnitSymbol = "";

        /// <summary>System parameters (mass, name, material...) have a "$TopSolid..." name.</summary>
        public bool IsSystem
        {
            get { return Name.StartsWith("$", StringComparison.Ordinal); }
        }

        public string Label
        {
            get { return string.IsNullOrEmpty(FriendlyName) ? Name : FriendlyName; }
        }

        /// <summary>Value for people: a length in mm, an angle in degrees, the rest as read.</summary>
        public string Display()
        {
            return DisplayValue(Kind, Value, UnitType, UnitSymbol);
        }

        public static string DisplayValue(string kind, string value, string unitType, string unitSymbol)
        {
            if (kind != "Real") return value;
            double v = DocumentSnapshot.Dbl(value);
            if (double.IsNaN(v)) return value;
            switch (unitType)
            {
                case "Length": return Num(v * 1000.0) + " mm";
                case "Angle": return Num(v * 180.0 / Math.PI) + " °";
                case "Mass": return Num(v) + " kg";
                case "Volume": return Num2(v * 1e9) + " mm3";
                case "Area": return Num2(v * 1e6) + " mm2";
                case "InertiaMoment": return Num(v) + " kg·m2";
                case "Pressure": return Num(v / 1e6) + " MPa";
                case "None": case "": return Num(v);
                default: return Num(v) + (string.IsNullOrEmpty(unitSymbol) ? "" : " (" + unitType + ")");
            }
        }

        internal static string Num(double v)
        {
            return Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string Num2(double v)
        {
            return Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>Same kind and same value (reals within 1e-9 relative).</summary>
        public bool SameValue(SnapshotParameter other)
        {
            if (other == null || other.Kind != Kind) return false;
            if (Kind != "Real") return other.Value == Value;
            double a = DocumentSnapshot.Dbl(Value), b = DocumentSnapshot.Dbl(other.Value);
            if (double.IsNaN(a) || double.IsNaN(b)) return other.Value == Value;
            return Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        }
    }

    public sealed class SnapshotOperation
    {
        public int Index;
        public int Id;
        public string Type = "";
        public string Name = "";
        public int OwnerId;
        public bool Active = true;
        public bool Frozen;
        public bool Invalid;
        public List<int> Children = new List<int>();

        /// <summary>Last segment of the type: "BossOperation", "DrillingGroupOperation"...</summary>
        public string ShortType
        {
            get
            {
                int i = Type.LastIndexOf('.');
                return i < 0 ? Type : Type.Substring(i + 1);
            }
        }

        /// <summary>"Départ (...)" operations only open a stage or a group: not compared.</summary>
        public bool IsStructural
        {
            get { return Type.EndsWith(".StartOperation", StringComparison.Ordinal); }
        }

        public string StateText
        {
            get { return (Active ? "active" : "inactive") + (Frozen ? ", frozen" : ""); }
        }
    }

    public sealed class SnapshotShape
    {
        public string Name = "";
        public int FaceCount;
        public bool Truncated;
        public Dictionary<string, int> Surfaces = new Dictionary<string, int>();
        public List<CylinderGroup> Cylinders = new List<CylinderGroup>();
    }

    public sealed class CylinderGroup
    {
        public double DiameterMm;
        public double LengthMm;
        public int Count;

        public string Key
        {
            get { return SnapshotParameter.Num(DiameterMm) + "|" + SnapshotParameter.Num(LengthMm); }
        }

        public override string ToString()
        {
            return Count + " × Ø" + SnapshotParameter.Num(DiameterMm) + " mm (L " + SnapshotParameter.Num(LengthMm) + " mm)";
        }
    }
}
