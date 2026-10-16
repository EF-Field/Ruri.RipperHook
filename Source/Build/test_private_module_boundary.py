"""Guard the public framework / owned private adapter split without private access."""
from pathlib import Path
import subprocess
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
RETIRED = 'Source/Ruri.GameHook'


class PrivateModuleBoundaryTests(unittest.TestCase):
    def test_submodule_registration(self):
        modules = (ROOT / '.gitmodules').read_text(encoding='utf-8')
        self.assertNotIn(RETIRED, modules)
        self.assertIn('Source/Endfield-GameHook', modules)

    def test_no_retired_build_inputs(self):
        for name in ('Ruri.RipperHook', 'Ruri.FModelHook'):
            project = ROOT / 'Source' / name / (name + '.csproj')
            for item in ET.parse(project).iter():
                for value in item.attrib.values():
                    self.assertNotIn('Ruri.GameHook', value, str(project))

    def test_framework_remains_source_not_submodule(self):
        rows = subprocess.check_output(
            ['git', '-C', str(ROOT), 'ls-files', '--stage'], text=True).splitlines()
        gitlinks = [row.split('\t', 1)[1] for row in rows if row.startswith('160000 ')]
        self.assertNotIn(RETIRED, gitlinks)
        self.assertNotIn('Source/Ruri.Hook', gitlinks)
        self.assertIn('Source/Endfield-GameHook', gitlinks)
        self.assertTrue((ROOT / 'Source/Ruri.Hook/RuriHook.cs').is_file())
        self.assertTrue((ROOT / 'Source/Ruri.Hook/Ruri.Hook.csproj').is_file())

    def test_tpk_default_preserves_private_pack(self):
        program = (ROOT / 'Source/Ruri.Tpk/Program.cs').read_text(encoding='utf-8')
        self.assertIn('Path.Combine(AppContext.BaseDirectory, "generated", TpkFileName)', program)
        layout = (ROOT / 'Source/Ruri.Tpk/Pipeline/RepoLayout.cs').read_text(encoding='utf-8')
        self.assertNotIn('Ruri.GameHook', layout)
        self.assertIn('"Endfield-GameHook", "Endfield"', layout)


if __name__ == '__main__':
    unittest.main()
