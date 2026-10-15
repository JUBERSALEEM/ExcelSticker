"""Optional source invariants; NOT a replacement for dotnet build or Windows UI tests.
Run with Python 3 + tree-sitter + tree-sitter-c-sharp installed.
"""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

root = Path(__file__).resolve().parents[1]
parser = Parser(Language(tree_sitter_c_sharp.language()))
cs = [p for folder in ("src", "tests") for p in (root / folder).rglob("*.cs") if not {"obj", "bin"}.intersection(p.parts)]
for path in cs:
    assert not parser.parse(path.read_bytes()).root_node.has_error, path
xml = [p for folder in ("src", "tests") for p in (root / folder).rglob("*") if p.suffix in (".xaml", ".csproj", ".pubxml") and not {"obj", "bin"}.intersection(p.parts)]
for path in xml + [root / "Directory.Build.props", root / "Directory.Build.targets"]:
    ET.parse(path)

def source(path):
    return (root / path).read_text(encoding="utf-8")

studio = source("src/ExcelSticker.Wpf/Studio/StickerEditorWindow.cs")
company = source("src/ExcelSticker.Wpf/Studio/StickerEditorWindow.Company.cs")
main = source("src/ExcelSticker.Wpf/MainWindow.xaml.cs")
assert "LostKeyboardFocus" not in studio
assert 'if(element.Field=="CustomText")Text("Text","Custom text")' in studio
assert "ApplyGeometry();ApplyCompany();" in studio and "if(HasPendingStock)ApplyStock();" in studio
assert "_company=company.Clone()" in studio
assert "_draft.Save(_savePath);SavedLibrary=_draft.Clone();_saved=true;" in company
assert "CompanyProfile.FromDetails(savedCompany):CompanyProfile.Load()" in main
assert "ApplyStickerLibrary(saved)" in main and "_layouts=saved.Clone()" in main
assert "JobPreview.Invalidate(" in main and "RenderPreview();RefreshSampleJobIfOpen();" in main
assert "Company=Company?.Clone()" in source("src/ExcelSticker.Core/LayoutLibrary.cs")
assert "item.LogoDarkness,item.LogoContrast,item.LogoBlackWhite,item.LogoThreshold" in source("src/ExcelSticker.Wpf/LabelView.cs")
assert "LogoTone.Validate(e.LogoDarkness,e.LogoContrast,e.LogoThreshold)" in source("src/ExcelSticker.Core/StickerLayout.cs")
assert "ToneCache.Count>=8" in source("src/ExcelSticker.Wpf/LogoImages.cs")
assert "CompanyAndToneChecks.Run();" in source("tests/ExcelSticker.Wpf.Tests/Program.cs")
# All literal reflected properties in the new editable company panel actually exist.
model = source("src/ExcelSticker.Core/CompanyDetails.cs")
for prop in ("Name", "Address", "Phone", "Website", "Email"):
    assert re.search(r"public string " + prop + r"\s*\{", model)
print(f"PASS static syntax: {len(cs)} C# files; {len(xml)+2} XML/project files")
print("PASS Studio input, persistence, company activation, renderer and test-registration source invariants")
print("NOT RUN: compilation, executable tests, Windows interaction, physical printing")
