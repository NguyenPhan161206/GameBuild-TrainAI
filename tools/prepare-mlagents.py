#!/usr/bin/env python3
"""Verified upstream 4.0.0 package with one Unity 6000.6 API compatibility patch."""
from pathlib import Path
import hashlib, tarfile, urllib.request, json
root=Path(__file__).resolve().parents[1]
url='https://packages.unity.com/com.unity.ml-agents/-/com.unity.ml-agents-4.0.0.tgz'
expected='d77df592d40ce32cd9d3d93ba31552ad7efc891c3339897d5f64324d3a526114'
archive=root/'artifacts/downloads/mlagents-4.0.0.tgz';archive.parent.mkdir(parents=True,exist_ok=True)
if not archive.exists():urllib.request.urlretrieve(url,archive)
if hashlib.sha256(archive.read_bytes()).hexdigest()!=expected:raise RuntimeError('ML-Agents archive checksum mismatch')
destination=root/'artifacts/packages/com.unity.ml-agents';destination.parent.mkdir(parents=True,exist_ok=True)
metadata=destination/'AEROLAB_PATCH.json'
if metadata.exists() and json.loads(metadata.read_text()).get('patch_revision')==2:
    print('Prepared ML-Agents compatibility package already exists');raise SystemExit(0)
with tarfile.open(archive,'r:gz') as tar:
    members=tar.getmembers()
    for member in members:
        path=Path(member.name)
        if not path.parts or path.parts[0]!='package' or '..' in path.parts or member.issym() or member.islnk():raise RuntimeError('Unsafe package archive member')
    tar.extractall(destination.parent,filter='data') if hasattr(tarfile,'data_filter') else tar.extractall(destination.parent)
unpacked=destination.parent/'package'
# Keep existing destination intact until verified extraction succeeds.
import shutil
if destination.exists():shutil.rmtree(destination)
unpacked.rename(destination)
p=destination/'Runtime/Integrations/Match3/Match3ActuatorComponent.cs'
s=p.read_text();old='var seed = m_RandomSeed == -1 ? gameObject.GetInstanceID() : m_RandomSeed + 1;'
assert s.count(old)==1
s=s.replace(old,'''// AeroLab compatibility: GetInstanceID is an error in Unity 6000.6.
#if UNITY_6000_6_OR_NEWER
            var seed = m_RandomSeed == -1 ? gameObject.GetEntityId().GetHashCode() : m_RandomSeed + 1;
#else
            '''+old+'''
#endif''');p.write_text(s)
native=destination/'Plugins/ProtoBuffer/runtimes/linux/native/libgrpc_csharp_ext.x64.so.meta'
meta=native.read_text();old_cpu='Standalone: Linux64\n    second:\n      enabled: 1\n      settings:\n        CPU: AnyCPU'
assert meta.count(old_cpu)==1
native.write_text(meta.replace(old_cpu,old_cpu.replace('CPU: AnyCPU','CPU: x86_64')))
(destination/'AEROLAB_PATCH.json').write_text(json.dumps({'upstream':url,'sha256':expected,'version':'4.0.0','patch_revision':2,'patch':'Conditional Match3 instance seed API for Unity 6000.6; explicit x86_64 Linux gRPC importer; no policy/trainer changes'},indent=2))
print('Prepared',destination)
