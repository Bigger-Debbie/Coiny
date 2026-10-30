import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import backup_bundle as bundle


class BackupBundleTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.source = self.root / 'snapshots'; self.source.mkdir()
        (self.source / 'control.dump').write_bytes(b'PGDMP synthetic central identities')
        (self.source / 'household.sqlite').write_bytes(b'synthetic financial snapshot')
        self.key = self.root / 'identity'
        subprocess.run(['age-keygen', '-o', str(self.key)], check=True, capture_output=True)
        self.recipient = subprocess.check_output(['age-keygen', '-y', str(self.key)], text=True).strip()
        self.package = self.root / 'backup.age'

    def tearDown(self):
        self.temporary.cleanup()

    def pack(self):
        return bundle.pack(self.source, self.package, self.recipient, 'synthetic-test', 'recovery-key-v1')

    def test_roundtrip_private_files_and_randomized_ciphertext(self):
        manifest = self.pack()
        other = self.root / 'other.age'
        bundle.pack(self.source, other, self.recipient, 'synthetic-test', 'recovery-key-v1')
        self.assertNotEqual(self.package.read_bytes(), other.read_bytes())
        self.assertNotIn(b'synthetic central', self.package.read_bytes())
        target = self.root / 'restored'
        self.assertEqual(manifest, bundle.unpack(self.package, target, self.key))
        for path in self.source.iterdir():
            self.assertEqual(path.read_bytes(), (target / path.name).read_bytes())
            self.assertEqual(0o600, (target / path.name).stat().st_mode & 0o777)
        self.assertEqual(0o700, target.stat().st_mode & 0o777)
        self.assertEqual(0o600, self.package.stat().st_mode & 0o777)

    def test_wrong_key_tamper_and_truncation_never_publish(self):
        self.pack(); original = self.package.read_bytes()
        other_key = self.root / 'wrong-key'
        subprocess.run(['age-keygen', '-o', str(other_key)], check=True, capture_output=True)
        target = self.root / 'restored'
        with self.assertRaises(ValueError): bundle.unpack(self.package, target, other_key)
        for data in [original[:-1], original[:50], original[:-10] + bytes([original[-10] ^ 1]) + original[-9:]]:
            self.package.write_bytes(data)
            with self.assertRaises(ValueError): bundle.unpack(self.package, target, self.key)
            self.assertFalse(target.exists())
        self.assertFalse(list(self.root.glob('.coiny-open-*')))

    def test_overwrite_and_interrupted_publication(self):
        self.pack(); before = self.package.read_bytes()
        with self.assertRaises(FileExistsError): self.pack()
        self.assertEqual(before, self.package.read_bytes())
        target = self.root / 'restored'; target.mkdir(); (target / 'keep').write_text('safe')
        with self.assertRaises(ValueError): bundle.unpack(self.package, target, self.key)
        self.assertEqual('safe', (target / 'keep').read_text())
        self.package.unlink()
        with patch.object(bundle, 'publish_file', side_effect=OSError('interrupted')):
            with self.assertRaises(OSError): self.pack()
        self.assertFalse(self.package.exists())
        self.assertFalse(list(self.root.glob('.coiny-pack-*')))

    def test_symlink_and_missing_central_dump_rejected(self):
        (self.source / 'link').symlink_to(self.key)
        with self.assertRaises(ValueError): self.pack()
        (self.source / 'link').unlink(); (self.source / 'control.dump').unlink()
        with self.assertRaises(ValueError): self.pack()
        self.assertFalse(self.package.exists())

    def test_authenticated_but_unsafe_archives_rejected(self):
        # An age recipient is public: encryption does not establish author identity.
        # Validate members even when the cryptographic envelope is valid.
        for name, kind in [('../escaped', tarfile.REGTYPE), ('control.dump', tarfile.SYMTYPE)]:
            archive = self.root / 'unsafe.tar'
            manifest = {'format': 1, 'release': 'test', 'key_id': 'v1', 'files': [
                {'name': 'control.dump', 'bytes': 1, 'sha256': '0' * 64}]}
            with tarfile.open(archive, 'w') as tar:
                raw = json.dumps(manifest).encode(); entry = tarfile.TarInfo('manifest.json'); entry.size = len(raw)
                tar.addfile(entry, io.BytesIO(raw))
                entry = tarfile.TarInfo(name); entry.type = kind; entry.size = 1 if kind == tarfile.REGTYPE else 0
                entry.linkname = '../escaped'
                tar.addfile(entry, io.BytesIO(b'x') if entry.size else None)
            with open(archive, 'rb') as incoming, open(self.package, 'wb') as outgoing:
                bundle.age(['-r', self.recipient], incoming, outgoing)
            target = self.root / 'restored'
            with self.assertRaises(ValueError): bundle.unpack(self.package, target, self.key)
            self.assertFalse(target.exists()); self.assertFalse((self.root / 'escaped').exists())


if __name__ == '__main__':
    unittest.main()
