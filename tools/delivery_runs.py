"""Portable run identity, verified checkpoint generations and durable JSON writes."""
import hashlib
import json
import os
import pathlib
import shutil
import tempfile
import time
import uuid


def digest(path):
    h = hashlib.sha256()
    with pathlib.Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def write_json(path, value):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    with temporary.open('w') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def environment_hash(root):
    root = pathlib.Path(root)
    files = []
    for folder in ['Assets/AeroLab/Core', 'Assets/AeroLab/Runtime', 'Assets/AeroLab/Editor']:
        files.extend((root / folder).rglob('*.cs'))
    files += [root / p for p in ['Packages/manifest.json', 'Packages/packages-lock.json',
                                'tools/learn-delivery.py', 'tools/delivery_checkpoint_hooks.py',
                                'tools/flight_feature_policy.py', 'tools/prepare-mlagents.py']]
    h = hashlib.sha256()
    for path in sorted(files):
        if path.is_file():
            h.update(str(path.relative_to(root)).encode() + b'\0' + path.read_bytes() + b'\0')
    return h.hexdigest()


def checkpoint_step(path, require_optimizer=True):
    # Only load trusted project-generated PyTorch artifacts.
    import torch
    data = torch.load(path, map_location='cpu')
    required = {'Policy', 'global_step'}
    if require_optimizer:
        required |= {'Optimizer:value_optimizer', 'Optimizer:critic'}
    if not required.issubset(data):
        raise ValueError('Checkpoint missing training modules: ' + str(required - data.keys()))
    step = int(next(iter(data['global_step'].values())))
    if step < 0:
        raise ValueError('Negative checkpoint step')
    return step


def snapshot(run_dir, behavior, step, backup=None):
    run_dir = pathlib.Path(run_dir)
    parent = run_dir / 'snapshots'
    parent.mkdir(exist_ok=True)
    temporary = pathlib.Path(tempfile.mkdtemp(prefix='.writing-', dir=parent))
    source = run_dir / behavior
    try:
        shutil.copy2(source / 'checkpoint.pt', temporary / 'checkpoint.pt')
        actual = checkpoint_step(temporary / 'checkpoint.pt')
        if actual != int(step):
            raise ValueError('Checkpoint step mismatch')
        shutil.copy2(source / (behavior + '-' + str(step) + '.onnx'), temporary / 'model.onnx')
        shutil.copy2(run_dir / 'run.json', temporary / 'run.json')
        shutil.copy2(run_dir / 'configuration.yaml', temporary / 'configuration.yaml')
        manifest = {'step': actual, 'behavior': behavior, 'created': time.time(),
                    'files': {p.name: digest(p) for p in temporary.iterdir()}}
        write_json(temporary / 'snapshot.json', manifest)
        destination = parent / (f'{actual:012d}-' + uuid.uuid4().hex[:8])
        os.replace(temporary, destination)
        verify_snapshot(destination)
        if backup:
            backup = pathlib.Path(backup) / run_dir.name
            backup.mkdir(parents=True, exist_ok=True)
            staging = backup / ('.writing-' + destination.name)
            shutil.copytree(destination, staging)
            verify_snapshot(staging)
            os.replace(staging, backup / destination.name)
            prune(backup)
        prune(parent)
        return destination
    finally:
        if temporary.exists():
            shutil.rmtree(temporary)


def verify_snapshot(folder):
    folder = pathlib.Path(folder)
    meta = json.loads((folder / 'snapshot.json').read_text())
    expected = {'checkpoint.pt', 'model.onnx', 'run.json', 'configuration.yaml'}
    if set(meta['files']) != expected:
        raise ValueError('Incomplete snapshot manifest')
    for name, checksum in meta['files'].items():
        if digest(folder / name) != checksum:
            raise ValueError('Invalid snapshot checksum: ' + name)
    if checkpoint_step(folder / 'checkpoint.pt') != meta['step']:
        raise ValueError('Snapshot step differs')
    return meta


def prune(parent):
    candidates = sorted((p for p in pathlib.Path(parent).iterdir()
                         if p.is_dir() and not p.name.startswith('.')), reverse=True)
    valid = []
    for folder in candidates:
        try:
            verify_snapshot(folder)
            valid.append(folder)
        except (ValueError, OSError, KeyError, RuntimeError):
            continue
    for folder in valid[3:]:
        shutil.rmtree(folder)


def restore(run_dir, behavior):
    run_dir = pathlib.Path(run_dir)
    failures = []
    identity = json.loads((run_dir / 'run.json').read_text())['identity']
    for folder in sorted((run_dir / 'snapshots').glob('*'), reverse=True):
        if folder.name.startswith('.'):
            continue
        try:
            meta = verify_snapshot(folder)
            saved = json.loads((folder / 'run.json').read_text())
            if meta['behavior'] != behavior or saved['identity'] != identity:
                raise ValueError('Snapshot identity mismatch')
            target = run_dir / behavior
            target.mkdir(exist_ok=True)
            shutil.copy2(folder / 'checkpoint.pt', target / 'checkpoint.pt')
            return meta['step'], failures
        except (ValueError, OSError, KeyError, RuntimeError) as error:
            failures.append({'snapshot': folder.name, 'reason': str(error)})
    raise ValueError('No valid full-training snapshot available: ' + str(failures))
