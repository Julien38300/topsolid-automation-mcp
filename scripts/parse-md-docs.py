#!/usr/bin/env python3
"""
Parse TopSolid Automation MD documentation into a compact JSON API index.
Input:  Directory of *_Method.md files from TopSolid'Design Automation doc
Output: api-index.json with all interfaces, methods, signatures, descriptions
"""

import os
import re
import json
import sys

# Known interfaces to split method names correctly
# e.g. IParametersGetRealValue -> (IParameters, GetRealValue)
KNOWN_INTERFACES = [
    "IAnnotations", "IApplication", "IAssemblies", "IBoms", "IClassifications",
    "ICoatings", "IDimensions", "IDocuments", "IDocumentsEvents", "IDraftings",
    "IElements", "IEntities", "IFamilies", "IFeatures", "IFinishings",
    "IGeometries2D", "IGeometries3D", "IHealing", "ILayers", "ILicenses",
    "IMaterials", "IMechanisms", "IMultiLayer", "IOperations", "IOptions",
    "IParameters", "IParts", "IPdm", "IPdmAdmin", "IPdmSecurity", "IPdmWorkflow",
    "IProcesses", "IRepresentations", "IShapes", "ISimulations",
    "ISketches2D", "ISketches3D", "IStyles", "ISubstitutions", "ITables",
    "ITextures", "ITools", "IUnfoldings", "IUnits", "IUser", "IVisualization3D",
]

# Sort longest first so IDocumentsEvents matches before IDocuments
KNOWN_INTERFACES.sort(key=len, reverse=True)


def split_interface_method(filename_stem):
    """Split 'IParametersGetRealValue' into ('IParameters', 'GetRealValue')"""
    for iface in KNOWN_INTERFACES:
        if filename_stem.startswith(iface):
            method = filename_stem[len(iface):]
            if method:
                return iface, method
    return None, None


def extract_description(content):
    """Extract the one-line description after the title block."""
    lines = content.split('\n')
    # Find the line after the second "TopSolid'Design Automation" that isn't empty or a table
    found_second = False
    for i, line in enumerate(lines):
        if "TopSolid'Design Automation" in line:
            if found_second:
                # Next non-empty, non-table, non-header line is the description
                for j in range(i + 1, min(i + 5, len(lines))):
                    candidate = lines[j].strip()
                    if candidate and not candidate.startswith('|') and not candidate.startswith('#') and not candidate.startswith('**'):
                        return candidate
                break
            found_second = True
    return ""


def extract_csharp_signature(content):
    """Extract the first C# code block (the signature)."""
    blocks = re.findall(r'```\n(.*?)```', content, re.DOTALL)
    if blocks:
        sig = blocks[0].strip()
        # Clean up: join multiline signatures
        sig = re.sub(r'\n\t+', ' ', sig)
        sig = re.sub(r'\s+', ' ', sig)
        return sig
    return ""


def extract_namespace(content):
    """Extract the namespace."""
    m = re.search(r'\*\*Namespace:\*\*\s*\n?\s*(\S+)', content)
    if m:
        ns = m.group(1).rstrip('\\').strip()
        return ns
    return ""


def extract_since(content):
    """Extract 'available since vX.Y' from Remarks."""
    m = re.search(r'available since (v[\d.]+)', content)
    if m:
        return m.group(1)
    return ""


def extract_return_description(content):
    """Extract the return value description."""
    m = re.search(r'#### Return Value\s*\n\s*Type:\s*(\S+)\\?\s*\n\s*(.*?)(?:\n\n|\nRemarks|\nSee Also)', content, re.DOTALL)
    if m:
        return_type = m.group(1).strip()
        return_desc = m.group(2).strip()
        return return_desc
    return ""


def parse_method_file(filepath):
    """Parse a single *_Method.md file."""
    with open(filepath, 'r', encoding='utf-8', errors='replace') as f:
        content = f.read()

    filename = os.path.basename(filepath)
    stem = filename.replace('_Method.md', '')

    iface, method_name = split_interface_method(stem)
    if not iface:
        return None

    signature = extract_csharp_signature(content)
    description = extract_description(content)
    namespace = extract_namespace(content)
    since = extract_since(content)

    return {
        "interface": iface,
        "name": method_name,
        "signature": signature,
        "description": description,
        "namespace": namespace,
        "since": since,
    }


def main():
    if len(sys.argv) < 2:
        md_dir = r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\TopSolid'Design Automation md"
    else:
        md_dir = sys.argv[1]

    output_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "api-index.json")

    print(f"Parsing MD files from: {md_dir}")
    print(f"Output: {output_path}")

    # Find all *_Method.md files
    method_files = [f for f in os.listdir(md_dir) if f.endswith('_Method.md')]
    print(f"Found {len(method_files)} method files")

    # Parse all files
    interfaces = {}
    skipped = 0
    for fname in sorted(method_files):
        filepath = os.path.join(md_dir, fname)
        result = parse_method_file(filepath)
        if result:
            iface = result["interface"]
            if iface not in interfaces:
                interfaces[iface] = {
                    "namespace": result["namespace"],
                    "methods": []
                }
            interfaces[iface]["methods"].append({
                "name": result["name"],
                "signature": result["signature"],
                "description": result["description"],
                "since": result["since"],
            })
        else:
            skipped += 1

    # Sort methods within each interface
    for iface in interfaces:
        interfaces[iface]["methods"].sort(key=lambda m: m["name"])

    # Build output
    index = {"interfaces": interfaces}

    # Stats
    total_methods = sum(len(v["methods"]) for v in interfaces.values())
    print(f"\nParsed: {total_methods} methods across {len(interfaces)} interfaces")
    print(f"Skipped: {skipped} files (no matching interface)")

    # Top interfaces
    print("\nTop interfaces by method count:")
    for iface, data in sorted(interfaces.items(), key=lambda x: -len(x[1]["methods"]))[:10]:
        print(f"  {iface}: {len(data['methods'])} methods")

    # Write output
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(index, f, indent=2, ensure_ascii=False)

    size_kb = os.path.getsize(output_path) / 1024
    print(f"\nOutput: {output_path} ({size_kb:.0f} KB)")


if __name__ == "__main__":
    main()
