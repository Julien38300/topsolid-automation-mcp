# -*- coding: utf-8 -*-
"""T-1066 étape 6 : preuve runtime de la troncature de la recette read_project_contents.

Extrait le corps C# de la recette depuis server/src/Tools/RecipeTool.cs, le compile
avec un stub TopSolidHost (arbre synthétique > 500 entrées) via csc Framework 4.8,
puis exécute et vérifie que la sortie est plafonnée à 500 entrées avec marquage
TRUNCATED. Zéro accès à TopSolid, zéro installation.
"""
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "server" / "src" / "Tools" / "RecipeTool.cs"
OUT = REPO / "cache" / "t1066_cap"
OUT.mkdir(exist_ok=True)

text = SRC.read_text(encoding="utf-8-sig")
m = re.search(r'\{ "read_project_contents", R\((.*?)\) \},', text, re.S)
assert m, "recette read_project_contents introuvable"
block = m.group(1)

# Extraire les littéraux "..." de la description et du corps (après le 2e argument).
literals = re.findall(r'"((?:[^"\\]|\\.)*)"', block)
# La description est le 2e argument : tous les littéraux sauf les 2 premiers (catégorie, description) forment le corps.
body = "".join(lit.encode().decode("unicode_escape") for lit in literals[2:])

assert "MaxEntries = 500" in body, "plafond MaxEntries absent du corps extrait"
assert "OUTPUT TRUNCATED" in body, "marquage TRUNCATED absent du corps extrait"

stub = r'''
using System;
using System.Collections.Generic;
public struct PdmObjectId { public bool IsEmpty; public int Id; public PdmObjectId(int id){Id=id;IsEmpty=false;} }
public static class TopSolidHost {
    public class Node { public string Name; public List<PdmObjectId> Docs = new List<PdmObjectId>(); public List<PdmObjectId> Folders = new List<PdmObjectId>(); }
    public static Dictionary<PdmObjectId, Node> Tree = new Dictionary<PdmObjectId, Node>();
    public class PdmApi {
        public PdmObjectId GetCurrentProject() { return new PdmObjectId(1); }
        public string GetName(PdmObjectId id) { return Tree[id].Name; }
        public void GetConstituents(PdmObjectId parent, out List<PdmObjectId> folders, out List<PdmObjectId> docs) {
            folders = Tree[parent].Folders; docs = Tree[parent].Docs; }
    }
    public static PdmApi Pdm = new PdmApi();
}
public static class Harness {
    public static string Body() {
        %BODY%
    }
    public static int Main() {
        // Arbre synthétique : 2 dossiers racine x (2 sous-dossiers x 200 docs) = 800+ entrées
        var root = new Node(); root.Name = "ProjRoot";
        for (int i = 0; i < 2; i++) {
            var f = new Node(); f.Name = "Folder" + i; root.Folders.Add(new PdmObjectId(f.GetHashCode()));
            Tree[f.GetHashCode()] = f;
            for (int j = 0; j < 2; j++) {
                var sf = new Node(); sf.Name = "Sub" + i + j; f.Folders.Add(new PdmObjectId(sf.GetHashCode()));
                Tree[sf.GetHashCode()] = sf;
                for (int k = 0; k < 200; k++) {
                    var d = new PdmObjectId(sf.GetHashCode() * 31 + k);
                    sf.Docs.Add(d); Tree[d] = new Node { Name = "Doc" + i + j + "_" + k };
                    var dd = new PdmObjectId(sf.GetHashCode() * 31 + k); // nom résolu via Tree
                    root.Docs.Add(dd);
                    Tree[dd].Name = "" ;
                }
            }
        }
        // reconstruire proprement : docs rangés dans leur sous-dossier
        int idc = 100000;
        foreach (var kv in new List<PdmObjectId>(Tree.Keys)) { }
        string outp = Body();
        int lines = outp.Split('\n').Length - 1;
        bool trunc = outp.Contains("OUTPUT TRUNCATED at 500");
        bool counts = outp.Contains("folders,");
        System.IO.File.WriteAllText("cache/t1066_cap/output.txt", outp);
        Console.WriteLine("LINES=" + lines);
        Console.WriteLine("TRUNC_MARK=" + trunc);
        Console.WriteLine("UNDER_550=" + (lines < 550 ? "OK" : "FAIL"));
        return (trunc && lines < 550 && counts) ? 0 : 1;
    }
}
'''

# Simpler, deterministic tree builder without hash collisions
stub = stub.replace('''        // Arbre synthétique : 2 dossiers racine x (2 sous-dossiers x 200 docs) = 800+ entrées
        var root = new Node(); root.Name = "ProjRoot";
        for (int i = 0; i < 2; i++) {
            var f = new Node(); f.Name = "Folder" + i; root.Folders.Add(new PdmObjectId(f.GetHashCode()));
            Tree[f.GetHashCode()] = f;
            for (int j = 0; j < 2; j++) {
                var sf = new Node(); sf.Name = "Sub" + i + j; f.Folders.Add(new PdmObjectId(sf.GetHashCode()));
                Tree[sf.GetHashCode()] = sf;
                for (int k = 0; k < 200; k++) {
                    var d = new PdmObjectId(sf.GetHashCode() * 31 + k);
                    sf.Docs.Add(d); Tree[d] = new Node { Name = "Doc" + i + j + "_" + k };
                    var dd = new PdmObjectId(sf.GetHashCode() * 31 + k); // nom résolu via Tree
                    root.Docs.Add(dd);
                    Tree[dd].Name = "" ;
                }
            }
        }
        // reconstruire proprement : docs rangés dans leur sous-dossier
        int idc = 100000;
        foreach (var kv in new List<PdmObjectId>(Tree.Keys)) { }''',
'''        // Arbre synthétique : 4 sous-dossiers x 250 docs = 1000 entrées
        var Tree = TopSolidHost.Tree;
        Func<int, string, TopSolidHost.Node> mk = (id, nm) => { var n = new TopSolidHost.Node(); n.Name = nm; Tree[new PdmObjectId(id)] = n; return n; };
        var rootN = mk(1, "ProjRoot");
        int idc = 10;
        for (int i = 0; i < 4; i++) {
            int fidv = idc++;
            var f = mk(fidv, "Folder" + i);
            rootN.Folders.Add(new PdmObjectId(fidv));
            for (int k = 0; k < 250; k++) {
                int didv = idc++;
                f.Docs.Add(new PdmObjectId(didv));
                mk(didv, "Doc" + i + "_" + k);
            }
        }''')

# Indenter le corps extrait (chaque ligne -> 8 espaces)
indented = "\n".join("        " + ln.strip("\n").replace("\"", "\\\"") if False else ln for ln in [body])
# Le corps contient déjà des quotes échappées propres ; l'insérer tel quel dans le template C# :
stub_final = stub.replace("%BODY%", body)

(Path(OUT.parent.parent) / "cache" / "t1066_cap" / "harness.cs").parent.mkdir(exist_ok=True)
(OUT / "harness.cs").write_text(stub_final, encoding="utf-8")
(OUT / "body.cs").write_text(body, encoding="utf-8")

CSC = r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
r1 = subprocess.run([CSC, "/nologo", "/out:" + str(OUT / "harness.exe"), str(OUT / "harness.cs")],
                    capture_output=True, text=True, cwd=str(REPO), encoding="cp1252", errors="replace")
print("CSC exit=", r1.returncode, (r1.stdout + r1.stderr).strip()[:400])
if r1.returncode != 0:
    sys.exit(1)
r2 = subprocess.run([str(OUT / "harness.exe")], capture_output=True, text=True, cwd=str(REPO))
print("RUN exit=", r2.returncode)
print(r2.stdout.strip())
outp = (OUT / "output.txt").read_text(encoding="utf-8")
print("HEAD:", outp.splitlines()[:4])
print("TAIL:", outp.splitlines()[-2:])
sys.exit(r2.returncode)
