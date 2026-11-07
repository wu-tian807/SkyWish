"""Strict static-object envelope around the MIT upstream FBX/texture validator."""
import json
import os
import re
import sys
from vendor.cs2_validate import validate


def inspect(directory):
    result = validate({"directory": directory})
    name = result["asset"]
    problems = result["problems"]
    if not re.fullmatch(r"CD_[A-Za-z][A-Za-z0-9]{0,39}[a-f0-9]{32}", name):
        problems.append("Use an immutable asset name: CD_<label><32 lowercase UUID hex digits>.")
    allowed = {name + ".fbx"} | {name + "_" + s + ".png" for s in ("BaseColor", "Normal", "MaskMap", "ControlMask", "Emissive")}
    for entry in os.scandir(directory):
        if not entry.is_file(follow_symlinks=False) or entry.name not in allowed:
            problems.append("First version supports one static mesh and its own textures only: " + entry.name)
    if len(result["meshes"]) != 1:
        problems.append("Exactly one mesh is supported in this milestone.")
    for mesh in result["meshes"]:
        if mesh["format"] != "binary" or not mesh["extent"] or max(mesh["extent"]) <= 0:
            problems.append("Mesh must contain readable, non-empty binary FBX geometry.")
        if mesh["models"] is None or len(mesh["models"]) != 1:
            problems.append("Export one mesh object only.")
        if mesh["materials"] != [name]:
            problems.append("The single material must have the same name as the asset.")
    result["ok"] = not problems
    result["preset"] = "StaticObject"
    return result


if __name__ == "__main__":
    try:
        print(json.dumps(inspect(sys.argv[1]), ensure_ascii=True))
    except Exception as exc:
        print(json.dumps({"ok": False, "problems": [str(exc)]}))
        sys.exit(1)
