"""Offline packaging only: inputs must already be coordinated, closed snapshots."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import resource
import shutil
import subprocess
import tarfile
import tempfile

MAX_BYTES = 16 * 1024**3
MAX_FILES = 10000


def digest(path):
    with open(path, 'rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def age(arguments, source, target):
    # No shell, passwords, private key contents or tool diagnostics in logs.
    def limit_output():
        resource.setrlimit(resource.RLIMIT_FSIZE, (MAX_BYTES + 32 * 1024**2,) * 2)
    result = subprocess.run(['age', *arguments], stdin=source, stdout=target,
                            stderr=subprocess.PIPE, timeout=3600, preexec_fn=limit_output)
    if result.returncode:
        raise ValueError('Encryption/decryption failed; check the key and package integrity.')


def private_file(path):
    return open(path, 'xb', opener=lambda p, flags: os.open(p, flags, 0o600))


def publish_file(staged, destination):
    # Same-filesystem hard-link publication refuses races/overwrites.
    os.link(staged, destination)
    with open(destination, 'rb') as output:
        os.fsync(output.fileno())
    parent = os.open(destination.parent, os.O_RDONLY)
    try:
        os.fsync(parent)
    finally:
        os.close(parent)


def pack(source, output, recipient, release, key_id):
    source, output = Path(source), Path(output)
    if not re.fullmatch(r'age1[a-z0-9]{58}', recipient):
        raise ValueError('Use a native age public recipient key.')
    if not release or not key_id:
        raise ValueError('Release and operational key version are required.')
    files = sorted(source.iterdir())
    if not files or len(files) > MAX_FILES:
        raise ValueError('Snapshot file count is invalid.')
    records, total = [], 0
    for path in files:
        if path.is_symlink() or not path.is_file() or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,150}', path.name) or path.name == 'manifest.json':
            raise ValueError('Snapshot directory must contain only flat, regular snapshot files.')
        total += path.stat().st_size
        records.append({'name': path.name, 'bytes': path.stat().st_size, 'sha256': digest(path)})
    if total > MAX_BYTES or not any(p.name == 'control.dump' for p in files):
        raise ValueError('A bounded snapshot containing control.dump is required.')
    manifest = {'format': 1, 'release': release, 'key_id': key_id,
                'recipient': recipient, 'secrets': 'not-included-restore-or-rotate-separately', 'files': records}
    # Private, same-filesystem staging; neither incomplete archive is public.
    with tempfile.TemporaryDirectory(prefix='.coiny-pack-', dir=output.parent) as temporary:
        temporary = Path(temporary)
        archive = temporary / 'snapshot.tar'
        with private_file(archive) as stream, tarfile.open(fileobj=stream, mode='w') as tar:
            raw = json.dumps(manifest, sort_keys=True).encode()
            entry = tarfile.TarInfo('manifest.json'); entry.size = len(raw); entry.mode = 0o600
            tar.addfile(entry, io.BytesIO(raw))
            for path in files:
                tar.add(path, arcname=path.name, recursive=False)
        # Verify the exact staged bytes, including detecting changed source files.
        validate_archive(archive)
        encrypted = temporary / 'snapshot.age'
        with open(archive, 'rb') as incoming, private_file(encrypted) as outgoing:
            age(['--encrypt', '--recipient', recipient], incoming, outgoing)
            outgoing.flush(); os.fsync(outgoing.fileno())
        publish_file(encrypted, output)
    return manifest


def validate_archive(archive, extract=None):
    with tarfile.open(archive, mode='r:') as tar:
        first = tar.next()
        if first is None or first.name != 'manifest.json' or not first.isfile() or first.size > 4 * 1024**2:
            raise ValueError('Missing or invalid manifest.')
        manifest = json.load(tar.extractfile(first))
        if manifest.get('format') != 1 or not manifest.get('release') or not manifest.get('key_id'):
            raise ValueError('Unsupported backup manifest.')
        records = manifest.get('files')
        if not isinstance(records, list) or not 0 < len(records) <= MAX_FILES:
            raise ValueError('Invalid file inventory.')
        expected = {}
        total = 0
        for record in records:
            name, size, sha = record['name'], record['bytes'], record['sha256']
            if not isinstance(name, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,150}', name) or name == 'manifest.json' or name in expected:
                raise ValueError('Unsafe or duplicate archive path.')
            if type(size) is not int or size < 0 or not re.fullmatch(r'[A-Fa-f0-9]{64}', sha):
                raise ValueError('Invalid file metadata.')
            total += size; expected[name] = record
        if total > MAX_BYTES or 'control.dump' not in expected:
            raise ValueError('Invalid snapshot inventory.')
        seen = set()
        while (entry := tar.next()) is not None:
            if not entry.isfile() or entry.name not in expected or entry.name in seen or entry.size != expected[entry.name]['bytes']:
                raise ValueError('Unexpected, unsafe or duplicate archive member.')
            seen.add(entry.name)
            hasher = hashlib.sha256()
            with tar.extractfile(entry) as incoming:
                outgoing = private_file(Path(extract) / entry.name) if extract else None
                try:
                    while chunk := incoming.read(1024**2):
                        hasher.update(chunk)
                        if outgoing: outgoing.write(chunk)
                finally:
                    if outgoing: outgoing.close()
            if hasher.hexdigest() != expected[entry.name]['sha256']:
                raise ValueError('Snapshot checksum mismatch.')
        if seen != set(expected):
            raise ValueError('Snapshot files are missing.')
        if extract:
            with private_file(Path(extract) / 'manifest.json') as stream:
                stream.write(json.dumps(manifest, sort_keys=True).encode())
        return manifest


def unpack(package, destination, identity):
    destination = Path(destination)
    if destination.exists():
        raise ValueError('Recovery requires a new destination directory.')
    with tempfile.TemporaryDirectory(prefix='.coiny-open-', dir=destination.parent) as temporary:
        temporary = Path(temporary)
        archive = temporary / 'snapshot.tar'
        with open(package, 'rb') as incoming, private_file(archive) as outgoing:
            age(['--decrypt', '--identity', str(identity)], incoming, outgoing)
        if archive.stat().st_size > MAX_BYTES + 32 * 1024**2:
            raise ValueError('Backup exceeds the recovery size limit.')
        extracted = temporary / 'verified'; extracted.mkdir(mode=0o700)
        manifest = validate_archive(archive, extracted)
        # mkdir reserves the destination exclusively; move only fully validated files.
        # Destination may briefly be empty, but never contains unauthenticated bytes.
        destination.mkdir(mode=0o700)
        try:
            for path in extracted.iterdir():
                os.link(path, destination / path.name)
        except BaseException:
            shutil.rmtree(destination)
            raise
        return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    create = commands.add_parser('pack')
    create.add_argument('--snapshots', required=True); create.add_argument('--output', required=True)
    create.add_argument('--recipient', required=True); create.add_argument('--release', required=True)
    create.add_argument('--key-id', required=True)
    recover = commands.add_parser('unpack')
    recover.add_argument('--package', required=True); recover.add_argument('--destination', required=True)
    recover.add_argument('--identity', required=True)
    args = parser.parse_args()
    try:
        if args.command == 'pack':
            result = pack(args.snapshots, args.output, args.recipient, args.release, args.key_id)
        else:
            result = unpack(args.package, args.destination, args.identity)
        print(f"Verified backup format {result['format']}: {len(result['files'])} files.")
    except (OSError, ValueError, KeyError, TypeError, tarfile.TarError, subprocess.SubprocessError):
        parser.exit(1, 'Backup operation failed. Check inputs, keys and available disk space; databases were not activated.\n')


if __name__ == '__main__':
    main()
