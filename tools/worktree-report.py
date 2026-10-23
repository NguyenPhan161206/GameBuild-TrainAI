#!/usr/bin/env python3
"""Read Git state and optionally render a local, dependency-free HTML snapshot."""

import argparse
from collections import Counter, defaultdict
from datetime import datetime
from html import escape
import json
from pathlib import Path
import subprocess


GROUPS = (
    ("Assets/", "Unity · mã và tài nguyên"),
    ("Packages/", "Unity · package"),
    ("ProjectSettings/", "Unity · cấu hình"),
    ("tools/", "Công cụ và kiểm tra"),
    ("training/", "Huấn luyện"),
    ("notebooks/", "Notebook"),
    ("docs/", "Tài liệu"),
)
CONFLICTS = {"DD", "AU", "UD", "UA", "DU", "AA", "UU"}
LABELS = {"M": "Sửa", "A": "Thêm", "D": "Xóa", "R": "Đổi tên",
          "C": "Sao chép", "T": "Đổi loại", "U": "Xung đột"}


def git(root, *args):
    return subprocess.check_output(
        ["git", "--no-optional-locks", "-C", str(root), *args],
        stderr=subprocess.PIPE,
    ).decode("utf-8", errors="replace")


def read_entries(root):
    records = iter(git(root, "status", "--porcelain=v1", "-z", "-uall").split("\0"))
    entries = []
    for record in records:
        if not record:
            continue
        status, path = record[:2], record[3:]
        old_path = next(records) if "R" in status or "C" in status else None
        group = next((label for prefix, label in GROUPS if path.startswith(prefix)),
                     "Gốc dự án và quy định")
        entries.append({"status": status, "path": path, "old_path": old_path,
                        "group": group, "new": status == "??",
                        "conflict": status in CONFLICTS,
                        "staged": status != "??" and status[0] != " ",
                        "unstaged": status != "??" and status[1] != " "})
    return entries


def preview(root, entry):
    if entry["new"]:
        path = root / entry["path"]
        if path.is_symlink():
            return "File mới là symbolic link; không đọc nội dung đích."
        if not path.is_file():
            return "Không có bản xem trước cho đường dẫn này."
        try:
            with path.open("rb") as stream:
                raw = stream.read(128 * 1024 + 1)
            if b"\0" in raw:
                return "File mới dạng nhị phân; chưa có bản gốc trong Git."
            content = raw[:128 * 1024].decode("utf-8", errors="replace")
            lines = content.splitlines()
            clipped = len(raw) > 128 * 1024 or len(lines) > 160
            return ("File mới · nội dung hiện tại (chưa có diff với Git)\n\n"
                    + "\n".join(lines[:160])
                    + ("\n… Đã rút gọn bản xem trước." if clipped else ""))
        except OSError as exc:
            return f"Không đọc được bản xem trước: {exc}"
    parts = []
    paths = [entry["path"]]
    if entry["old_path"]:
        paths.append(entry["old_path"])
    for label, extra in (("Đã stage · HEAD → index", ["--cached"]),
                         ("Chưa stage · index → working tree", [])):
        diff = git(root, "diff", "--no-ext-diff", "--no-textconv", "--no-color",
                   *extra, "--", *paths)
        if diff:
            lines = diff.splitlines()
            parts.append(label + "\n\n" + "\n".join(lines[:240])
                         + ("\n… Đã rút gọn diff." if len(lines) > 240 else ""))
    return "\n\n".join(parts) or "Không có diff văn bản (có thể là submodule hoặc metadata)."


def badges(entry):
    if entry["new"]:
        return "Mới · chưa được Git theo dõi"
    if entry["conflict"]:
        return "Xung đột"
    labels = []
    if entry["staged"]:
        labels.append("Đã stage: " + LABELS.get(entry["status"][0], entry["status"][0]))
    if entry["unstaged"]:
        labels.append("Chưa stage: " + LABELS.get(entry["status"][1], entry["status"][1]))
    return " · ".join(labels)


def preview_html(root, entry):
    lines = []
    for line in preview(root, entry).splitlines():
        kind = ""
        if not entry["new"]:
            if line.startswith("+") and not line.startswith("+++"):
                kind = "added"
            elif line.startswith("-") and not line.startswith("---"):
                kind = "removed"
            elif line.startswith(("@@", "diff ", "index ", "---", "+++")):
                kind = "context"
        lines.append(f"<span class='{kind}'>{escape(line)}</span>")
    return "\n".join(lines)


def render(root, entries, branch, history, generated):
    counts = Counter(entry["group"] for entry in entries)
    metrics = [("File thay đổi", len(entries)),
               ("File mới chưa theo dõi", sum(e["new"] for e in entries)),
               ("File có phần đã stage", sum(e["staged"] for e in entries)),
               ("File có phần chưa stage", sum(e["unstaged"] for e in entries)),
               ("Xung đột", sum(e["conflict"] for e in entries))]
    cards = "".join(f"<div class='card'><strong>{value}</strong>{escape(label)}</div>"
                    for label, value in metrics)
    chart = "".join(f"<div class='barrow'><span>{escape(group)}</span>"
                    f"<meter min='0' max='{max(counts.values(), default=1)}' value='{count}'></meter>"
                    f"<b>{count}</b></div>" for group, count in counts.items())
    groups = defaultdict(list)
    for entry in entries:
        search = escape(entry["path"].lower(), quote=True)
        kinds = " ".join(key for key in ("new", "staged", "unstaged", "conflict") if entry[key])
        path = entry["path"]
        if entry["old_path"]:
            path = entry["old_path"] + " → " + path
        groups[entry["group"]].append(
            f"<details class='file' data-path='{search}' data-kinds='{kinds}'>"
            f"<summary><code>{escape(entry['status'])}</code> <span>{escape(path)}</span>"
            f"<small>{escape(badges(entry))}</small></summary>"
            f"<pre>{preview_html(root, entry)}</pre></details>")
    files = "".join(f"<section class='group'><h2>{escape(group)} <small>{len(rows)} file</small></h2>"
                    + "".join(rows) + "</section>" for group, rows in groups.items())
    return """<!doctype html>
<html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>AeroLab · Thay đổi cục bộ</title>
<style>
:root{color-scheme:dark;font:16px/1.55 system-ui,sans-serif;background:#101720;color:#e3ebf4}
body{max-width:1180px;margin:35px auto;padding:0 22px}h1{font-size:32px;margin-bottom:6px}
h2{font-size:20px;margin-top:0}p{color:#b9cadc}code,pre{font-family:ui-monospace,monospace}
.meta{color:#9ab3cb;font-size:14px;overflow-wrap:anywhere}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(155px,1fr));gap:12px;margin:26px 0}
.card,section{background:#192331;border:1px solid #304257;border-radius:12px;padding:18px}
.card{font-size:14px;color:#bfd0e0}.card strong{display:block;font-size:32px;color:#83deca}
section{margin:18px 0}.barrow{display:grid;grid-template-columns:minmax(190px,1fr) 2fr 40px;gap:16px;align-items:center;margin:9px 0}
meter{width:100%;height:20px;accent-color:#61cbb2}small{color:#a9c0d5;font-size:13px}
.controls{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0}input,select{padding:12px;border-radius:8px;border:1px solid #536c85;background:#192331;color:#e3ebf4;font:inherit}
input{flex:1;min-width:160px}details{border-top:1px solid #34465c;padding:12px 0}summary{cursor:pointer;overflow-wrap:anywhere}
summary code{white-space:pre;color:#8ee8cf;margin-right:10px}summary small{display:block;margin-left:34px}
pre{overflow:auto;max-height:540px;background:#0e1520;padding:16px;border-radius:8px;font-size:13px;tab-size:4}
.added{color:#8ee8b0}.removed{color:#ffaaa8}.context{color:#8ebff5}
.note{border-left:3px solid #d6b377;padding:8px 16px}[hidden]{display:none!important}
@media(max-width:600px){.barrow{grid-template-columns:1fr 1fr 26px;gap:8px;font-size:13px}body{padding:0 12px}}
</style>
<h1>Thay đổi cục bộ · AeroLab</h1>
""" + f"<div class='meta'>{escape(str(root))}<br>{escape(branch)}<br>Cập nhật: {escape(generated)}</div>" + """
<p>Xem nhanh phần đang làm, mở từng file để đọc diff hoặc nội dung file mới.</p>
""" + f"<div class='cards'>{cards}</div><section><h2>Thay đổi theo nhóm</h2>{chart or 'Working tree sạch.'}</section>" + """
<p class="note">Đây là ảnh chụp lúc tạo báo cáo. Chạy lại <code>python3 tools/worktree-report.py --html</code> rồi tải lại trang để cập nhật.
File mới chưa có bản gốc trong Git; báo cáo không phải bản sao lưu. File đã stage vẫn có thể còn phần chưa stage, nên các ô đếm có thể giao nhau.</p>
<div class="controls"><input id="search" aria-label="Tìm theo đường dẫn" placeholder="Tìm theo đường dẫn…">
<select id="kind" aria-label="Lọc trạng thái"><option value="">Tất cả trạng thái</option><option value="new">Mới · chưa theo dõi</option>
<option value="staged">Có phần đã stage</option><option value="unstaged">Có phần chưa stage</option><option value="conflict">Xung đột</option></select></div>
<p id="visible" aria-live="polite"></p>
""" + files + f"""
<section><h2>Commit gần đây · local</h2><pre>{escape(history)}</pre>
<p>Thông tin ahead/behind dùng remote-tracking ref có sẵn trên máy; lệnh này không fetch, pull, stage, commit hay push.</p></section>
<p>Builds, cache, model và kết quả bị .gitignore loại trừ không nằm trong danh sách. Mở file bằng trình soạn thảo để xem đầy đủ nếu bản xem trước bị rút gọn.</p>
<script>
const search=document.getElementById('search'),kind=document.getElementById('kind');
function filter(){{let count=0;document.querySelectorAll('.file').forEach(e=>{{
 const show=e.dataset.path.includes(search.value.toLowerCase())&&(!kind.value||e.dataset.kinds.split(' ').includes(kind.value));
 e.hidden=!show;if(show)count++;
}});document.querySelectorAll('.group').forEach(g=>g.hidden=![...g.querySelectorAll('.file')].some(e=>!e.hidden));
document.getElementById('visible').textContent='Đang hiển thị '+count+' file';}}
search.addEventListener('input',filter);kind.addEventListener('change',filter);filter();
</script></html>"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--html", action="store_true", help="Tạo artifacts/worktree/index.html")
    args = parser.parse_args()
    root = Path(git(Path(__file__).resolve().parent, "rev-parse", "--show-toplevel").strip())
    entries = read_entries(root)
    branch = git(root, "status", "--short", "--branch", "--untracked-files=no").splitlines()[0]
    history = git(root, "log", "-6", "--format=%h %s")
    generated = datetime.now().astimezone().isoformat(timespec="seconds")
    print(f"{branch}\nCập nhật: {generated}\n{len(entries)} file thay đổi")
    for group in dict.fromkeys(e["group"] for e in entries):
        rows = [e for e in entries if e["group"] == group]
        print(f"\n{group} ({len(rows)})")
        for entry in rows:
            print(f"  {entry['status']} {json.dumps(entry['path'], ensure_ascii=False)}")
    print("\n?? = file mới chưa theo dõi; cột 1 = index/staged; cột 2 = working tree/chưa stage.")
    print("Remote dùng dữ liệu local; không fetch. Không thay đổi index, commit hoặc nhánh.")
    if args.html:
        target = root / "artifacts/worktree/index.html"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(render(root, entries, branch, history, generated), encoding="utf-8")
        print(f"\nBảng xem: {target}")


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as exc:
        raise SystemExit(exc.stderr.decode("utf-8", errors="replace"))
