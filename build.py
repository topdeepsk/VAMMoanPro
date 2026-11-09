# -*- coding: utf-8 -*-
"""
VAMMoanPlugin 打包脚本：把本项目打包成 VaM 可用的 .var

用法:
    python build.py

流程:
  1. 读 meta.json，取 creatorName / packageName / buildVersion，缺一个就报错；
  2. 扫 src/ 下所有 .cs，确保是 UTF-8 with BOM；
  3. 校验 audio/jsondb 的结构完整性，并逐条检查它引用的音频文件是否真的存在、
     扩展名与文件头是否一致（wav 认 RIFF，ogg 认 OggS，aiff 认 FORM+AIFF，
     mp1/mp2/mp3 认 ID3 或帧同步字）；任何一项不通过就中止打包，不产生半成品包；
  4. 把这些 .cs 的相对路径写进 VAMMoanPlugin.cslist，并写进 meta.json 的 contentList；
  5. 把 meta.json + VAMMoanPlugin.cslist + src/**.cs + audio/ 下所有文件（任意后缀、含子目录）
     打成 dist/<creatorName>.<packageName>.<buildVersion>.var

说明:
  - VaM 的包版本号只认文件名三段（creator.package.version），meta.json 里没有
    version 字段；buildVersion 是本项目版本号的唯一来源，只改 meta.json 即可。
  - var 内路径固定为 Custom/Scripts/<packageName>/...，与散装目录结构一致。
  - 产物只写到项目内 dist/，不会碰 VaM 的 AddonPackages。
"""
import json
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
META_PATH = os.path.join(ROOT, "meta.json")
DIST_DIR = os.path.join(ROOT, "dist")

SRC_DIR = "src"
CSLIST = "VAMMoanPlugin.cslist"
AUDIO_DIR = "audio"

# 音频布局（插件运行时按这个结构读取）
JSONDB_DIR = os.path.join(AUDIO_DIR, "jsondb")                    # audio/jsondb/<音色>/voice.json
VOICES_DIR = os.path.join(AUDIO_DIR, "voicebundle", "voices")     # audio/voicebundle/voices/<音色>/
SHARED_DIR = os.path.join(AUDIO_DIR, "voicebundle", "voices-shared")  # 共用音效，按类别分子目录
# BASS 内核原生支持的格式（不需要附加 dll，VaM 只带 bass.dll）。
# 顺序即优先级：同一个文件名有多种格式时，取排在前面的那个。
# FLAC / AAC / WMA 需要 bassflac 等插件，VaM 没有自带，所以不收。
AUDIO_EXTS = (".wav", ".ogg", ".aiff", ".aif", ".aifc", ".mp3", ".mp2", ".mp1")

# 文件头（magic bytes）：确认"名字对得上"的文件真的是可播放的音频。
# 每个扩展名是一组必须同时满足的条件：[(偏移, (允许的签名...)), ...]
AUDIO_MAGIC = {
    ".wav":  ((0, (b"RIFF",)),),
    ".ogg":  ((0, (b"OggS",)),),
    ".aiff": ((0, (b"FORM",)), (8, (b"AIFF", b"AIFC"))),
    ".aif":  ((0, (b"FORM",)), (8, (b"AIFF", b"AIFC"))),
    ".aifc": ((0, (b"FORM",)), (8, (b"AIFC",))),
}

# MPEG 音频（mp1/mp2/mp3）：要么带 ID3 标签，要么帧同步字 0xFFEx / 0xFFFx
MPEG_AUDIO_EXTS = (".mp1", ".mp2", ".mp3")

# 需要额外解码器（VaM 未自带）的格式：只提示，不当作可用素材
UNSUPPORTED_EXTS = (".flac", ".wma", ".aac", ".m4a", ".ape", ".opus")



# jsondb 结构要求
CHAR_LIST_KEYS = ("moans", "orgasms", "perpetual_orgasms")
CHAR_SETTING_KEYS = ("gender", "intensitiesCount", "moanRepetitionBuffer", "overlapLengths")
SHARED_GENRES = ("female", "male")
SHARED_GENRE_KEYS = ("kisses", "breathes", "blowjobs")
SHARED_TOP_KEYS = ("pelvicslaps", "squishes", "squishes_penis")

# var 内的插件根目录（必须在 Custom/Scripts 下，插件浏览器只从这里列条目）
SCRIPTS_ROOT = "Custom/Scripts"

# 不参与扫描的目录（dist 放产物，其余是开发中间产物）
EXCLUDE_DIRS = {".vs", "obj", "bin", "dist", "DesignTimeBuild", "FileContentIndex", "v18"}

# VaM 用 '.' 拆分 uid，所以作者名/包名里不能有点，也不该有路径分隔符
ID_BAD_CHARS = set('.:/\\')
ID_SAFE = re.compile(r"^[A-Za-z0-9_-]+$")


def die(msg):
    print("错误: " + msg)
    sys.exit(1)


def load_meta():
    if not os.path.isfile(META_PATH):
        die("找不到 " + META_PATH)
    try:
        with open(META_PATH, encoding="utf-8-sig") as f:
            return json.load(f)
    except ValueError as ex:
        die("meta.json 不是合法 JSON: %s" % ex)


def save_meta(meta):
    with open(META_PATH, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=3)
        f.write("\n")


def parse_version(raw):
    """buildVersion -> int；容忍 "3" / 3 / " 3 "。"""
    text = str(raw).strip()
    if not re.match(r"^\d+$", text):
        die("meta.json 的 buildVersion 不是合法的版本号（必须是纯十进制整数）: %r" % (raw,))
    return int(text)


def check_id(field, value):
    if not value:
        die("meta.json 缺少 " + field)
    if set(value) & ID_BAD_CHARS:
        die("%s=%r 含有点或路径分隔符；VaM 按 creator.package.version 三段拆分文件名，"
            "名称里不能出现这些字符" % (field, value))
    if not ID_SAFE.match(value):
        print("警告: %s=%r 含非 [A-Za-z0-9_-] 字符，建议用纯 ASCII" % (field, value))


def to_var_path(path):
    """磁盘绝对路径 -> 相对项目根、正斜杠的形式（如 src/Audio/AudioBank.cs）。"""
    return os.path.relpath(path, ROOT).replace("\\", "/")


def ensure_bom(path):
    """确保文件是 UTF-8 with BOM；返回是否改动。含中文的源码没有 BOM 时 mcs 可能误判编码。"""
    with open(path, "rb") as f:
        data = f.read()
    if data.startswith(b"\xef\xbb\xbf"):
        return False
    try:
        data.decode("utf-8")
    except UnicodeDecodeError as ex:
        die("%s 不是合法 UTF-8（第 %d 字节附近），请先转码: %s"
            % (to_var_path(path), ex.start, ex.reason))
    with open(path, "wb") as f:
        f.write(b"\xef\xbb\xbf" + data)
    return True


def collect_cs_files():
    """扫 src/ 下所有 .cs，返回排序后的绝对路径列表。"""
    base = os.path.join(ROOT, SRC_DIR)
    if not os.path.isdir(base):
        die("找不到源码目录: " + SRC_DIR)
    files = []
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for fn in filenames:
            if fn.lower().endswith(".cs"):
                files.append(os.path.join(dirpath, fn))
    files.sort(key=to_var_path)
    if not files:
        die("src/ 下没有 .cs 文件")
    return files


def collect_audio_files():
    """audio/ 下的所有文件（任意后缀，含子目录）。"""
    base = os.path.join(ROOT, AUDIO_DIR)
    files = []
    if not os.path.isdir(base):
        return files
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
        for fn in filenames:
            files.append(os.path.join(dirpath, fn))
    files.sort(key=to_var_path)
    return files


# ======================================================================
# 音频校验：jsondb 结构 + 引用到的音频文件是否存在
# ======================================================================

def read_jsondb(path):
    """读 jsondb；返回 (data, 提示)。解析失败返回 (None, 错误信息)。

    VaM 的 SimpleJSON 容忍尾逗号，标准 json 不容忍，所以失败后去掉尾逗号再试一次。
    """
    try:
        with open(path, encoding="utf-8-sig") as f:
            text = f.read()
    except OSError as ex:
        return None, "读取失败: %s" % ex

    try:
        return json.loads(text), None
    except ValueError:
        pass

    try:
        return json.loads(re.sub(r",(\s*[}\]])", r"\1", text)), "含尾逗号（VaM 能读，标准 JSON 不合法）"
    except ValueError as ex:
        return None, "非法 JSON: %s" % ex


def audio_stem(path):
    """'Assets/VAMMoan/Abby/m0-01.ogg' -> 'm0-01'（同时兼容反斜杠）。"""
    base = path.replace("\\", "/").rsplit("/", 1)[-1]
    return os.path.splitext(base)[0]


def index_audio(root, max_depth):
    """建「文件名（小写、不含扩展名）→ 磁盘路径」索引；max_depth 为向下展开层数。

    同一个名字有多种格式时，按 AUDIO_EXTS 的顺序取优先者（wav > ogg > aiff > …），
    避免 os.walk 的遍历顺序决定最终用哪个文件。
    """
    index = {}
    if not os.path.isdir(root):
        return index

    rank = dict((ext, i) for i, ext in enumerate(AUDIO_EXTS))
    root = os.path.normpath(root)
    for cur, dirnames, filenames in os.walk(root):
        rel = os.path.relpath(cur, root)
        depth = 0 if rel == "." else rel.count(os.sep) + 1
        if depth > max_depth:
            dirnames[:] = []
            continue

        for fn in filenames:
            stem, ext = os.path.splitext(fn)
            ext = ext.lower()
            if ext not in rank:
                continue

            key = stem.lower()
            prev = index.get(key)
            if prev is None or rank[os.path.splitext(prev)[1].lower()] > rank[ext]:
                index[key] = os.path.join(cur, fn)
    return index


def collect_refs(node, out=None):
    """递归收集所有 "audio" 字段的值。"""
    if out is None:
        out = []
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "audio" and isinstance(value, str):
                out.append(value)
            else:
                collect_refs(value, out)
    elif isinstance(node, list):
        for item in node:
            collect_refs(item, out)
    return out


def sniff_audio(path):
    """按扩展名核对文件头。返回 True 表示格式正确（或扩展名不认识，不判断）。"""
    ext = os.path.splitext(path)[1].lower()

    try:
        with open(path, "rb") as f:
            head = f.read(16)
    except OSError:
        return False

    # MPEG 音频：ID3 标签，或帧同步字（0xFF 后接 0xE? / 0xF?）
    if ext in MPEG_AUDIO_EXTS:
        if head.startswith(b"ID3"):
            return True
        return len(head) >= 2 and head[0] == 0xFF and (head[1] & 0xE0) == 0xE0

    conditions = AUDIO_MAGIC.get(ext)
    if not conditions:
        return True

    for offset, signatures in conditions:
        if not any(head[offset:offset + len(sig)] == sig for sig in signatures):
            return False
    return True


def check_refs(label, refs, index, errors, limit=5):
    """检查一批引用是否都能在 index 里找到，且文件头是合法音频；返回缺失数量。"""
    missing = []
    broken = []
    checked = set()

    for ref in refs:
        path = index.get(audio_stem(ref).lower())
        if not path:
            missing.append(ref)
            continue
        if path in checked:
            continue
        checked.add(path)
        if not sniff_audio(path):
            broken.append(path)

    if missing:
        errors.append("%s: %d/%d 条音频找不到文件，例如: %s"
                      % (label, len(missing), len(refs), ", ".join(missing[:limit])))
    if broken:
        errors.append("%s: %d 个文件扩展名与内容不符（可能被改名/截断），例如: %s"
                      % (label, len(broken), ", ".join(os.path.basename(p) for p in broken[:limit])))
    return len(missing)


def validate_audio():
    """打包前校验。任何错误都会终止打包（返回 False）。

    说明：jsondb 里的 "audio" 是原 AssetBundle 里的资源名（形如
    Assets/VAMMoan/Abby/m3-34.ogg），扩展名只是历史遗留，插件运行时不使用它 ——
    取用时只拿 GetFileNameWithoutExtension 的结果去匹配 AudioClip.name。
    所以这里也只用「文件名（不含扩展名）」匹配磁盘文件，允许 BASS 内核支持的
    格式互换（wav / ogg / aiff / aif / aifc / mp3 / mp2 / mp1），
    另外再核对文件头，避免"名字对上但文件是坏的"。
    """
    errors = []
    warnings = []

    jsondb_root = os.path.join(ROOT, JSONDB_DIR)
    voices_root = os.path.join(ROOT, VOICES_DIR)
    shared_root = os.path.join(ROOT, SHARED_DIR)

    if not os.path.isdir(jsondb_root):
        die("找不到 jsondb 目录: %s" % JSONDB_DIR)
    if not os.path.isdir(voices_root):
        die("找不到音色音频目录: %s" % VOICES_DIR)
    if not os.path.isdir(shared_root):
        die("找不到共用音频目录: %s" % SHARED_DIR)

    # ---- 角色音色 ----
    char_names = sorted(d for d in os.listdir(jsondb_root)
                        if os.path.isdir(os.path.join(jsondb_root, d)))
    if not char_names:
        errors.append("%s 下没有任何音色目录" % JSONDB_DIR)

    for name in char_names:
        jp = os.path.join(jsondb_root, name, "voice.json")
        if not os.path.isfile(jp):
            errors.append("%s: 缺少 voice.json" % name)
            continue

        data, note = read_jsondb(jp)
        if data is None:
            errors.append("%s: %s" % (name, note))
            continue
        if note:
            warnings.append("%s/voice.json %s" % (name, note))

        # 结构检查
        for key in CHAR_LIST_KEYS:
            value = data.get(key)
            if not isinstance(value, list) or not value:
                errors.append("%s: 缺少或为空的 \"%s\" 列表" % (name, key))

        settings = data.get("config", {}).get("settings", {})
        for key in CHAR_SETTING_KEYS:
            if key not in settings:
                errors.append("%s: config.settings 缺少 \"%s\"" % (name, key))

        gender = settings.get("gender")
        if gender is not None and str(gender) not in ("0", "1"):
            errors.append("%s: settings.gender=%r，应为 0(女) 或 1(男)" % (name, gender))

        # 音频文件存在性
        refs = collect_refs(data)
        char_audio_dir = os.path.join(voices_root, name.lower())
        if not os.path.isdir(char_audio_dir):
            errors.append("%s: 缺少音频目录 %s" % (name, os.path.join(VOICES_DIR, name.lower())))
        check_refs(name, refs, index_audio(char_audio_dir, 0), errors)

        moans = data.get("moans", [])

        # minArousal / maxArousal 必须成对且不乱序
        for i, entry in enumerate(moans if isinstance(moans, list) else []):
            if not isinstance(entry, dict):
                errors.append("%s: moans[%d] 不是对象" % (name, i))
                break
            if "minArousal" not in entry or "maxArousal" not in entry:
                errors.append("%s: moans[%d] 缺少 minArousal/maxArousal" % (name, i))
                break
            try:
                lo = float(entry["minArousal"])
                hi = float(entry["maxArousal"])
            except (TypeError, ValueError):
                errors.append("%s: moans[%d] 的 arousal 不是数字" % (name, i))
                break
            if lo > hi:
                errors.append("%s: moans[%d] minArousal(%s) > maxArousal(%s)" % (name, i, lo, hi))
                break

    # ---- 共用音效 ----
    shared_path = os.path.join(jsondb_root, "voice-shared.json")
    if not os.path.isfile(shared_path):
        errors.append("缺少共用配置 jsondb/voice-shared.json")
    else:
        data, note = read_jsondb(shared_path)
        if data is None:
            errors.append("voice-shared.json: %s" % note)
        else:
            if note:
                warnings.append("voice-shared.json %s" % note)

            for genre in SHARED_GENRES:
                block = data.get(genre)
                if not isinstance(block, dict):
                    errors.append("voice-shared.json: 缺少 \"%s\" 段" % genre)
                    continue
                for key in SHARED_GENRE_KEYS:
                    value = block.get(key)
                    if not isinstance(value, list) or not value:
                        errors.append("voice-shared.json: %s.%s 缺少或为空" % (genre, key))

            for key in SHARED_TOP_KEYS:
                value = data.get(key)
                if not isinstance(value, list) or not value:
                    errors.append("voice-shared.json: 缺少或为空的 \"%s\"" % key)

            refs = collect_refs(data)
            check_refs("共用音效", refs, index_audio(shared_root, 2), errors)

    # ---- 不支持的格式：给出明确提示，避免被误读成"文件丢了" ----
    unsupported = []
    for root in (voices_root, shared_root):
        for cur, _, filenames in os.walk(root):
            for fn in filenames:
                if os.path.splitext(fn)[1].lower() in UNSUPPORTED_EXTS:
                    unsupported.append(os.path.relpath(os.path.join(cur, fn), ROOT))
    if unsupported:
        warnings.append("发现 %d 个 BASS 内核不能直接解码的文件（建议转成 ogg），已跳过: %s"
                        % (len(unsupported),
                           ", ".join(os.path.basename(p) for p in unsupported[:3])))

    # ---- 孤儿文件（有文件但没被任何 jsondb 引用），只提示 ----
    referenced = set()
    for name in char_names:
        jp = os.path.join(jsondb_root, name, "voice.json")
        if os.path.isfile(jp):
            data, _ = read_jsondb(jp)
            if data:
                referenced.update(audio_stem(r).lower() for r in collect_refs(data))
    if os.path.isfile(shared_path):
        data, _ = read_jsondb(shared_path)
        if data:
            referenced.update(audio_stem(r).lower() for r in collect_refs(data))

    orphans = 0
    for root in (voices_root, shared_root):
        for cur, _, filenames in os.walk(root):
            for fn in filenames:
                stem, ext = os.path.splitext(fn)
                if ext.lower() in AUDIO_EXTS and stem.lower() not in referenced:
                    orphans += 1
    if orphans:
        warnings.append("有 %d 个音频文件没有被任何 jsondb 引用（不影响使用，只是冗余）" % orphans)

    for w in warnings:
        print("警告: " + w)

    if errors:
        print("")
        print("音频校验失败（%d 项）:" % len(errors))
        for e in errors[:20]:
            print("  - " + e)
        if len(errors) > 20:
            print("  ... 还有 %d 项" % (len(errors) - 20))
        return False

    return True


def write_cslist(lines):
    """cslist 每行一个 .cs 路径，相对 cslist 自身所在目录；VaM 逐行读取并一起编译。"""
    with open(os.path.join(ROOT, CSLIST), "w", encoding="utf-8", newline="\n") as f:
        for line in lines:
            f.write(line + "\n")


def main():
    # ---- 1. 读 meta.json ----
    meta = load_meta()
    creator = str(meta.get("creatorName") or "").strip()
    package = str(meta.get("packageName") or "").strip()
    if "buildVersion" not in meta:
        die("meta.json 缺少 buildVersion")
    check_id("creatorName", creator)
    check_id("packageName", package)
    version = parse_version(meta["buildVersion"])

    uid = "%s.%s.%d" % (creator, package, version)
    prefix = "%s/%s/" % (SCRIPTS_ROOT, package)

    # ---- 2. 源码：补 BOM ----
    cs_files = collect_cs_files()
    changed = [to_var_path(p) for p in cs_files if ensure_bom(p)]
    if changed:
        print("已补 UTF-8 BOM: %d 个文件" % len(changed))
        for p in changed:
            print("  " + p)

    # ---- 3. 音频校验：jsondb 结构 + 引用到的音频文件 ----
    if not validate_audio():
        die("音频校验未通过，已中止打包（未写入任何产物）")

    # ---- 4. 生成 cslist + contentList ----
    cslist_lines = [to_var_path(p) for p in cs_files]
    write_cslist(cslist_lines)

    audio_files = collect_audio_files()

    # contentList 只登记"有意义的资产"：入口 cslist + 源码文件。
    content = [prefix + CSLIST]
    content += [prefix + line for line in cslist_lines]
    content.sort()
    meta["contentList"] = content
    save_meta(meta)

    print("cslist: %d 个源码文件" % len(cslist_lines))
    if audio_files:
        print("audio: %d 个文件" % len(audio_files))
    else:
        print("提示: audio/ 下没有文件，本次包内只有脚本")

    # ---- 4. 打包 ----
    entries = [("meta.json", META_PATH), (prefix + CSLIST, os.path.join(ROOT, CSLIST))]
    entries += [(prefix + to_var_path(p), p) for p in cs_files]
    entries += [(prefix + to_var_path(p), p) for p in audio_files]
    for rel, _ in entries:
        if rel != "meta.json" and not rel.startswith(prefix):
            die("内部错误: %s 不在 %s 下" % (rel, prefix))

    os.makedirs(DIST_DIR, exist_ok=True)
    out = os.path.join(DIST_DIR, uid + ".var")

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for rel, full in entries:
            z.write(full, rel)
        names = sorted(z.namelist())

    # 回读校验：根层 meta.json、入口 cslist 必须存在，contentList 必须全部在包里
    if "meta.json" not in names:
        die("产物里缺少根层 meta.json")
    if prefix + CSLIST not in names:
        die("产物里缺少入口 %s" % (prefix + CSLIST))
    missing = [c for c in content if c not in names]
    if missing:
        die("contentList 里有 %d 项不在包内，例如 %s" % (len(missing), missing[0]))

    print("")
    print("打包完成: %s" % out)
    print("  大小      : %.1f KB (%d 个文件，含 meta.json)" % (os.path.getsize(out) / 1024.0, len(entries)))
    print("  包 uid    : %s" % uid)
    print("  插件入口  : %s:/%s%s" % (uid, prefix, CSLIST))


if __name__ == "__main__":
    main()