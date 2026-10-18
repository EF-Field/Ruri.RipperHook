"""Public-only MSBuild profile tests; no private repository or dependency binaries required."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'Source/Ruri.RipperHook/Ruri.RipperHook.csproj'


@unittest.skipUnless(shutil.which('dotnet'), 'Requires the .NET SDK')
class GameAdapterCompositionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='public-adapter-contract-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'Private Adapter'
        self.root.mkdir()
        (self.root / 'Hook.cs').write_text('class FixtureAdapter {}')
        (self.root / 'GameHook.targets').write_text('''<Project>
          <ItemGroup><Compile Include="$(MSBuildThisFileDirectory)Hook.cs" /></ItemGroup>
        </Project>''')

    def evaluate(self, properties, success=True):
        result = subprocess.run(['dotnet', 'msbuild', str(PROJECT), '-nologo',
                                 '-t:ValidatePrivateSourceSelection', '-getItem:Compile,EmbeddedResource'] +
                                ['-p:' + key + '=' + value for key, value in properties.items()],
                                text=True, encoding='utf-8', capture_output=True)
        if not success:
            self.assertNotEqual(result.returncode, 0, result.stdout)
            return result.stdout + result.stderr
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def fixture_compiled(self, result):
        return any(Path(row['FullPath']) == self.root / 'Hook.cs' for row in result['Items']['Compile'])

    def test_default_private_root_uses_canonical_submodule_name(self):
        result = subprocess.run(['dotnet', 'msbuild', str(PROJECT), '-nologo',
                                 '-getProperty:GameHookRoot'],
                                text=True, encoding='utf-8', capture_output=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(Path(result.stdout.strip()).resolve(), ROOT / 'Source/KWZE.GameHook')

    def test_generic_external_private_root(self):
        result = self.evaluate({'UseGameHook': 'true', 'GameHookRoot': str(self.root)})
        self.assertTrue(self.fixture_compiled(result))

    def test_old_switch_remains_alias(self):
        result = self.evaluate({'UseEndfieldGameHook': 'true', 'GameHookRoot': str(self.root)})
        self.assertTrue(self.fixture_compiled(result))

    def test_root_alone_does_not_opt_in(self):
        self.assertFalse(self.fixture_compiled(self.evaluate({'GameHookRoot': str(self.root)})))

    def test_public_release_never_imports_private_manifest(self):
        (self.root / 'GameHook.targets').write_text('<not-valid-xml')
        self.assertFalse(self.fixture_compiled(self.evaluate({'PureRelease': 'true', 'GameHookRoot': str(self.root)})))

    def test_public_private_conflict(self):
        for switch in ('UseGameHook', 'UseEndfieldGameHook'):
            with self.subTest(switch=switch):
                error = self.evaluate({switch: 'true', 'PureRelease': 'true', 'GameHookRoot': str(self.root)}, success=False)
                self.assertIn('PureRelease excludes private sources', error)

    def test_conflicting_enable_switches(self):
        self.assertIn('conflicts with UseGameHook', self.evaluate({
            'UseEndfieldGameHook': 'true', 'UseGameHook': 'false', 'GameHookRoot': str(self.root)}, success=False))

    def test_missing_private_root(self):
        self.assertIn('set GameHookRoot', self.evaluate({
            'UseGameHook': 'true', 'GameHookRoot': str(self.root / 'Missing')}, success=False))

    def test_old_pinned_statement_entry(self):
        (self.root / 'GameHook.targets').unlink()
        (self.root / 'Integration').mkdir()
        (self.root / 'Integration/Statement.targets').write_text('''<Project>
          <ItemGroup><Compile Include="$(MSBuildThisFileDirectory)../Hook.cs" /></ItemGroup>
        </Project>''')
        self.assertTrue(self.fixture_compiled(self.evaluate({'UseGameHook': 'true', 'GameHookRoot': str(self.root)})))
        self.assertIn('older adapter supports only', self.evaluate({
            'UseGameHook': 'true', 'GameHookRoot': str(self.root), 'GameHookGame': 'Other'}, success=False))
        self.assertIn('only Statement', self.evaluate({
            'UseGameHook': 'true', 'GameHookRoot': str(self.root), 'GameHookAbi': 'Legacy52'}, success=False))


if __name__ == '__main__':
    unittest.main()
