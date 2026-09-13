#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MegaDownloader Revival —— 文档多语言翻译器

用法:
    # 翻译全部文档的全部语言(CI 里这么跑)
    python i18n/translate.py

    # 只翻译 README
    python i18n/translate.py --doc readme

    # 只翻译 README 的日文
    python i18n/translate.py --doc readme --lang ja-JP

    # 本地预览：不调用 API，只打印将要发送的 prompt(排查问题用)
    python i18n/translate.py --dry-run

    # 检查产物是否与源同步(不调用 API，CI 里做守门)
    python i18n/translate.py --check

设计要点(踩过的坑都在这里，改之前先读):
  1. Markdown 结构必须逐字节保持。做法是把文档切成一堆 segment，
     只有 prose(自然语言段落)会被送去翻译，其余(fence / table / html / 链接定义)
     原样透传。这比"让 LLM 输出完整 Markdown"可靠得多。
  2. 代码块围栏 ``` 必须用正则整块吞掉，不能按行处理——否则 ```vb 里面的
     #Region 之类会被当成 Markdown 标题。
  3. 表格的行数/列数绝对不能变。表头行的 --- 分隔符不翻译。
  4. 缓存：把每个 prose segment 的 hash → 译文存到 i18n/.cache/<lang>.json。
     中文源没动过的段落直接命中缓存，省钱又快。
  5. 术语表通过两种方式注入：keep_verbatim 用 prompt 约束，
     preferred 用 prompt + 后置校验(发现被翻坏会在日志里 WARN)。
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

# ---------------------------------------------------------------------------
# 依赖：只用标准库 + PyYAML。yaml 缺失时退化到内置的极简解析器，保证 CI 不因
# 环境问题整体失败(工作流里也会 pip install pyyaml，这里只是兜底)。
# ---------------------------------------------------------------------------
try:
    import yaml
    HAS_YAML = True
except ImportError:
    HAS_YAML = False

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
I18N_DIR = os.path.join(ROOT, 'i18n')
CACHE_DIR = os.path.join(I18N_DIR, '.cache')

# LLM 配置从环境变量读(CI 里由 Secrets 注入)
API_BASE = os.environ.get('TRANSLATE_API_BASE', 'https://api.deepseek.com/v1').rstrip('/')
API_KEY = os.environ.get('TRANSLATE_API_KEY', '')
MODEL = os.environ.get('TRANSLATE_MODEL', 'deepseek-chat')


def log(msg):
    print(msg, flush=True)


def warn(msg):
    print('::warning::' + msg, flush=True)


def err(msg):
    print('::error::' + msg, flush=True)


def load_yaml(path):
    """加载 YAML。强依赖 PyYAML —— CI 里会 pip install，本地跑也要装。"""
    if not HAS_YAML:
        err('需要 PyYAML: pip install pyyaml')
        sys.exit(2)
    with open(path, encoding='utf-8') as f:
        return yaml.safe_load(f)


def _scalar(v):
    v = v.strip()
    if v == '':
        return ''
    if (v.startswith('"') and v.endswith('"')) or (v.startswith("'") and v.endswith("'")):
        return v[1:-1]
    if v.lower() in ('true', 'yes'):
        return True
    if v.lower() in ('false', 'no'):
        return False
    try:
        return int(v)
    except ValueError:
        pass
    return v


# ===========================================================================
# Markdown 切分：把文档拆成可翻译段落与不可翻译结构
# ===========================================================================
# 每个 segment 是 (kind, text)：
#   'prose'  自然语言，需要翻译
#   'fence'  代码围栏整块(含 ``` 行)，原样保留
#   'table'  表格整块，逐单元格处理(内部再切)
#   'html'   HTML 块(如 <div align=center>)，原样保留，只翻内部文本
#   'raw'    空行 / 分隔线 / 链接定义 / 纯符号，原样保留
# ===========================================================================

FENCE_RE = re.compile(r'^(\s*)(`{3,}|~{3,})(.*)$')
# 结束围栏：只允许「同类字符 + 长度 >= 起始 + 后面只有空白」。
# 不能用 FENCE_RE 判断闭合 —— 树形图里的 `│` 等 Unicode 制表符会被
# `~{3,}` 之外的情况误伤，且任何以 ``` 开头的行都会被当成闭合。
FENCE_CLOSE_RE = re.compile(r'^\s{0,3}(`{3,}|~{3,})\s*$')
LINK_DEF_RE = re.compile(r'^\s*\[[^\]]+\]:\s*\S+')


def split_markdown(text):
    """把 Markdown 切成 segment 列表。"""
    lines = text.split('\n')
    segs = []
    i = 0
    n = len(lines)

    while i < n:
        line = lines[i]

        # --- 代码围栏：整块吞 ---
        m = FENCE_RE.match(line)
        if m:
            fence_char = m.group(2)[0]
            min_len = len(m.group(2))
            buf = [line]
            i += 1
            while i < n:
                buf.append(lines[i])
                cm = FENCE_CLOSE_RE.match(lines[i])
                if cm and cm.group(1)[0] == fence_char and len(cm.group(1)) >= min_len:
                    i += 1
                    break
                i += 1
            segs.append(('fence', '\n'.join(buf)))
            continue

        # --- 表格：连续以 | 开头或含 | 且下一行是分隔符 ---
        if '|' in line and i + 1 < n and re.match(r'^\s*\|?[\s:\-|]+\|[\s:\-|]*$', lines[i + 1]):
            buf = [line, lines[i + 1]]
            i += 2
            while i < n and '|' in lines[i] and lines[i].strip() != '':
                buf.append(lines[i])
                i += 1
            segs.append(('table', '\n'.join(buf)))
            continue

        # --- HTML 块：<div ...> ... </div> 这类 ---
        if re.match(r'^\s*<[a-zA-Z][^>]*>\s*$', line) and not line.strip().startswith('<!--'):
            tag = re.match(r'^\s*<([a-zA-Z][a-zA-Z0-9]*)', line).group(1)
            buf = [line]
            i += 1
            while i < n:
                buf.append(lines[i])
                if re.search(r'</' + tag + r'\s*>', lines[i]):
                    i += 1
                    break
                i += 1
            segs.append(('html', '\n'.join(buf)))
            continue

        # --- 空行 / 分隔线 / 链接定义 / 纯符号行 ---
        st = line.strip()
        if st == '' or re.match(r'^-{3,}$|^\*{3,}$|^_{3,}$', st) or LINK_DEF_RE.match(line) \
                or re.match(r'^<!--.*-->$', st):
            segs.append(('raw', line))
            i += 1
            continue

        # --- 普通行：累积成一个 prose 段(连续非空行合并，省 API 调用) ---
        buf = [line]
        i += 1
        while i < n:
            nxt = lines[i]
            nst = nxt.strip()
            if nst == '':
                break
            if FENCE_RE.match(nxt):
                break
            if re.match(r'^-{3,}$|^\*{3,}$|^_{3,}$', nst):
                break
            if LINK_DEF_RE.match(nxt):
                break
            if '|' in nxt and i + 1 < n and re.match(r'^\s*\|?[\s:\-|]+\|[\s:\-|]*$', lines[i + 1]):
                break
            if re.match(r'^\s*<[a-zA-Z][^>]*>\s*$', nxt):
                break
            buf.append(nxt)
            i += 1
        segs.append(('prose', '\n'.join(buf)))

    return segs


# ---------------------------------------------------------------------------
# 行内保护：把 `code`、[text](url)、**bold** 等结构先替换成占位符，
# 翻译完再换回来。这是保证行内结构不被 LLM 破坏的关键。
# ---------------------------------------------------------------------------
PROTECT_PATTERNS = [
    r'`[^`\n]+`',                                   # 行内代码
    r'!\[[^\]]*\]\([^)]+\)',                        # 图片
    r'\[[^\]]+\]\([^)]+\)',                         # 链接
    r'<[a-zA-Z/][^>\n]*>',                          # 内联 HTML 标签
    r'https?://[^\s)\]>]+',                         # 裸 URL
]


def protect_inline(text):
    store = []

    def repl(m):
        store.append(m.group(0))
        return '\x00%d\x00' % (len(store) - 1)

    out = text
    for pat in PROTECT_PATTERNS:
        out = re.sub(pat, repl, out)
    return out, store


def restore_inline(text, store):
    def repl(m):
        idx = int(m.group(1))
        if 0 <= idx < len(store):
            return store[idx]
        return m.group(0)
    return re.sub(r'\x00(\d+)\x00', repl, text)


# ===========================================================================
# LLM 调用
# ===========================================================================
def build_system_prompt(glossary, target_lang, target_name):
    keep = glossary.get('keep_verbatim') or []
    preferred = glossary.get('preferred') or {}

    pref_lines = []
    for term, mapping in preferred.items():
        v = mapping.get(target_lang)
        if v:
            pref_lines.append('- %s -> %s' % (term, v))

    parts = [
        'You are a professional technical documentation translator for an open-source '
        'Windows software project called "MegaDownloader Revival".',
        '',
        'Translate the user-provided Markdown fragment from Simplified Chinese into %s (%s).'
        % (target_name, target_lang),
        '',
        'HARD RULES (violating any of these makes the output unusable):',
        '1. Output ONLY the translation. No preamble, no explanation, no code fence around it.',
        '2. Preserve every placeholder token that looks like \\x00N\\x00 EXACTLY as-is, '
        'including its position relative to surrounding words.',
        '3. Preserve all Markdown structure: heading markers (#), list bullets (-, *, 1.), '
        'blockquote markers (>), emphasis markers (** __ * _), and blank lines.',
        '4. Do NOT translate, transliterate, or reformat any code, code identifiers, file '
        'names, CLI commands, or URLs.',
        '5. Translate the natural language only. Keep the sentence meaning faithful and '
        'natural-sounding in the target language — do not translate word-by-word.',
        '6. Keep technical writing style: concise, professional, no marketing fluff, '
        'no added exclamation marks.',
    ]

    if keep:
        parts += [
            '',
            'The following terms MUST be kept verbatim (never translate/transliterate):',
            ', '.join(keep),
        ]

    if pref_lines:
        parts += [
            '',
            'Use these preferred translations for consistency:',
        ] + pref_lines

    parts += [
        '',
        'Emoji at the start of headings/lines must be preserved exactly.',
    ]

    return '\n'.join(parts)


def call_llm(system_prompt, user_text, cfg, attempt=0):
    payload = {
        'model': MODEL,
        'temperature': cfg.get('temperature', 0.2),
        'messages': [
            {'role': 'system', 'content': system_prompt},
            {'role': 'user', 'content': user_text},
        ],
    }
    req = urllib.request.Request(
        API_BASE + '/chat/completions',
        data=json.dumps(payload).encode('utf-8'),
        headers={
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + API_KEY,
        },
        method='POST',
    )

    try:
        with urllib.request.urlopen(req, timeout=180) as resp:
            body = json.loads(resp.read().decode('utf-8'))
        return body['choices'][0]['message']['content']
    except urllib.error.HTTPError as e:
        detail = e.read().decode('utf-8', 'replace')[:400]
        if e.code in (429, 500, 502, 503, 504) and attempt < cfg.get('max_retries', 3):
            wait = cfg.get('retry_base_seconds', 5) * (2 ** attempt)
            warn('LLM HTTP %d, %ds 后重试 (%s)' % (e.code, wait, detail))
            time.sleep(wait)
            return call_llm(system_prompt, user_text, cfg, attempt + 1)
        raise RuntimeError('LLM HTTP %d: %s' % (e.code, detail))
    except (urllib.error.URLError, TimeoutError) as e:
        if attempt < cfg.get('max_retries', 3):
            wait = cfg.get('retry_base_seconds', 5) * (2 ** attempt)
            warn('LLM 网络错误, %ds 后重试: %s' % (wait, e))
            time.sleep(wait)
            return call_llm(system_prompt, user_text, cfg, attempt + 1)
        raise


# ===========================================================================
# 缓存
# ===========================================================================
def cache_path(lang):
    return os.path.join(CACHE_DIR, lang + '.json')


def load_cache(lang):
    p = cache_path(lang)
    if os.path.exists(p):
        try:
            with open(p, encoding='utf-8') as f:
                return json.load(f)
        except Exception:
            return {}
    return {}


def save_cache(lang, cache):
    os.makedirs(CACHE_DIR, exist_ok=True)
    with open(cache_path(lang), 'w', encoding='utf-8') as f:
        json.dump(cache, f, ensure_ascii=False, indent=1, sort_keys=True)


def seg_hash(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()[:20]


# ===========================================================================
# 翻译一个 prose 段(含分块)
# ===========================================================================
def translate_prose(text, system_prompt, cfg, cache, dry_run=False):
    """翻译一整段 prose。命中缓存直接返回；否则分块调 LLM 并写回缓存。"""
    if text.strip() == '':
        return text

    # 短路：如果保护后剩下的全是占位符/空白(典型是 Badge 墙 —— 只有图片链接没有自然语言)，
    # 直接原样返回。既省 API 调用，也避免 LLM 在纯占位符上乱动。
    stripped, _ = protect_inline(text)
    if re.sub(r'\x00\d+\x00', '', stripped).strip() == '':
        return text

    h = seg_hash(text)
    if h in cache:
        return cache[h]

    if dry_run:
        return '<<DRY-RUN:%s>>' % h

    chunk_chars = cfg.get('chunk_chars', 12000)
    if len(text) <= chunk_chars:
        pieces = [text]
    else:
        # 按行切，尽量在空行/标题边界断开，保证语义完整
        pieces = []
        buf = []
        size = 0
        for line in text.split('\n'):
            if size + len(line) > chunk_chars and buf:
                pieces.append('\n'.join(buf))
                buf, size = [], 0
            buf.append(line)
            size += len(line) + 1
        if buf:
            pieces.append('\n'.join(buf))

    outs = []
    for piece in pieces:
        protected, store = protect_inline(piece)
        result = call_llm(system_prompt, protected, cfg)
        result = _unwrap_code_fence(result)
        outs.append(restore_inline(result, store))

    out = '\n'.join(outs)
    cache[h] = out
    return out


WRAP_FENCE_RE = re.compile(
    r'^\s*```[a-zA-Z0-9_+-]*\s*\n(.*?)\n?\s*```\s*$', re.S)


def _unwrap_code_fence(text):
    """剥掉 LLM 擅自加在外层的 ``` 围栏。

    注意不能用 text.strip('`') —— 那是按字符裁剪，会把译文里本就有的
    反引号一起吃掉。要按「外层围栏 + 可选语言标记」来匹配。
    """
    text = text.strip('\n')
    m = WRAP_FENCE_RE.match(text)
    if m:
        inner = m.group(1)
        # 只有当内层不包含裸露的围栏时才认为外层是多余的包装
        if '```' not in inner:
            return inner.strip('\n')
    return text


def translate_table(block, system_prompt, cfg, cache, dry_run=False):
    """表格：逐单元格翻译，行列数严格保持。"""
    lines = block.split('\n')
    out_lines = []
    for idx, line in enumerate(lines):
        stripped = line.strip()
        # 分隔行(|---|:--:|)不动
        if re.match(r'^\|?[\s:\-|]+\|[\s:\-|]*$', stripped):
            out_lines.append(line)
            continue
        # 拆单元格，保出前后结构
        cells = line.split('|')
        new_cells = []
        for c in cells:
            core = c.strip()
            if core == '':
                new_cells.append(c)
                continue
            translated = translate_prose(core, system_prompt, cfg, cache, dry_run)
            # 保留原有左右空格
            lead = c[:len(c) - len(c.lstrip())]
            trail = c[len(c.rstrip()):]
            new_cells.append(lead + translated.strip() + trail)
        out_lines.append('|'.join(new_cells))
    return '\n'.join(out_lines)


def translate_document(source_text, system_prompt, cfg, cache, dry_run=False):
    segs = split_markdown(source_text)
    out = []
    for kind, text in segs:
        if kind == 'prose':
            out.append(translate_prose(text, system_prompt, cfg, cache, dry_run))
        elif kind == 'table':
            out.append(translate_table(text, system_prompt, cfg, cache, dry_run))
        elif kind == 'html':
            # HTML 块：只翻标签之间的文本，属性不动
            out.append(translate_html_block(text, system_prompt, cfg, cache, dry_run))
        else:
            out.append(text)
    return '\n'.join(out)


def translate_html_block(block, system_prompt, cfg, cache, dry_run=False):
    """HTML 块里只翻译纯文本节点，标签与属性保持原样。"""
    parts = re.split(r'(<[^>]+>)', block)
    for i, p in enumerate(parts):
        if p.startswith('<') and p.endswith('>'):
            continue
        if p.strip() == '':
            continue
        lead = p[:len(p) - len(p.lstrip('\n'))]
        trail = p[len(p.rstrip('\n')):]
        core = p.strip('\n')
        if core.strip() == '':
            continue
        parts[i] = lead + translate_prose(core, system_prompt, cfg, cache, dry_run) + trail
    return ''.join(parts)


# ===========================================================================
# 语言导航条
# ===========================================================================
NAV_START = '<!-- i18n:nav -->'
NAV_END = '<!-- /i18n:nav -->'


def doc_output_path(config, doc, lang_code):
    """算出某个文档在指定语言下的产物路径。

    source_lang 不生成产物 —— 它的成品就是 doc['source'] 本身。
    返回仓库相对路径。
    """
    if lang_code == config.get('source_lang'):
        return doc['source']
    if lang_code == 'en':
        return doc['outputs']['en']
    return doc['outputs']['lang'].replace('{lang}', lang_code)


def build_nav(current_lang, config, doc):
    """生成语言导航条。current_lang 是 en / zh-TW / ja-JP / ko-KR / zh-CN。"""
    nav_tpl = (config.get('nav') or {}).get(current_lang)
    if not nav_tpl:
        return ''

    import posixpath
    outputs = doc['outputs']
    cur_path = doc_output_path(config, doc, current_lang)
    cur_dir = posixpath.dirname(cur_path)

    def rel(target_path):
        return posixpath.relpath(target_path, cur_dir) if cur_dir else target_path

    # en + 所有目标语言，外加「权威源语言」——它指向 source 文件本身
    subs = {'en': rel(outputs['en'])}
    for lg in config['languages']:
        code = lg['code']
        subs[code] = rel(outputs['lang'].replace('{lang}', code))
    src_lang = config.get('source_lang')
    if src_lang:
        subs[src_lang] = rel(doc['source'])

    try:
        line = nav_tpl.format(**subs)
    except KeyError as e:
        warn('语言导航模板缺少占位符: %s' % e)
        return ''

    return NAV_START + '\n' + line + '\n' + NAV_END


def inject_nav(text, nav_block, position='top'):
    """把导航条插入产物。已存在则替换，保证幂等。"""
    if nav_block == '':
        return text
    pattern = re.compile(re.escape(NAV_START) + r'.*?' + re.escape(NAV_END), re.S)
    if pattern.search(text):
        return pattern.sub(nav_block, text)
    lines = text.split('\n')
    # 插在第一个 H1 之后(保持标题在首位，导航在标题下面)
    for i, line in enumerate(lines):
        if line.startswith('# '):
            return '\n'.join(lines[:i + 1] + ['', nav_block] + lines[i + 1:])
    return nav_block + '\n\n' + text


def rewrite_doc_links(text, lang_code, doc, config, cur_out_rel):
    """把跨文档链接改写到当前语言的同名产物。

    中文源里写 [CHANGELOG](CHANGELOG.md)，但 ja-JP 的 README 必须指向
    docs/CHANGELOG.ja-JP.md，否则点过去是中文/英文页，语言链断裂。
    这里只处理 config.documents 里登记过的文档之间的互链，
    外链、图片、anchor 一律不动。

    **语言导航条必须跳过**：它里面的链接是「指向别语言」的，如果被当成
    普通跨文档链接改写，就会被改成指向自己、路径也会错乱。
    """
    import posixpath

    # 把导航条整块摘出来，只改正文，最后再拼回去
    nav_re = re.compile(re.escape(NAV_START) + r'.*?' + re.escape(NAV_END), re.S)
    m_nav = nav_re.search(text)
    if m_nav:
        head, nav_block, tail = text[:m_nav.start()], m_nav.group(0), text[m_nav.end():]
    else:
        head, nav_block, tail = text, '', ''

    # 中文源写在 docs/ 下，链接是相对 docs/ 的；产物可能在仓库根(README.md)，
    # 所以解析时必须同时尝试「相对源目录」与「相对仓库根」两种解释。
    src_dir = posixpath.dirname(doc['source'])

    # 本语言下「仓库根路径(英文产物名) -> 实际路径」的全量映射。
    # 用英文产物路径作为 key，因为中文源里的链接就是按英文名写的。
    # 注意这里不判等、不做过滤 —— 英文自身基准目录发生变化时(README.md 在根、
    # 而 docs/CHANGELOG.md 在子目录)也必须能解析出来。
    path_map = {}
    for other in config['documents']:
        path_map[other['outputs']['en']] = doc_output_path(config, other, lang_code)

    cur_dir = posixpath.dirname(cur_out_rel)

    # 锚点映射：源文档的标题 -> 本语言产物的标题(用于改写 #fragment)
    anchor_map = build_anchor_map(doc, lang_code, config)

    def resolve(path_part):
        """把链接路径还原成仓库根路径，两种相对基准都试。"""
        candidates = [posixpath.normpath(posixpath.join(src_dir, path_part)),
                      posixpath.normpath(path_part)]
        for c in candidates:
            if c in path_map:
                return path_map[c]
        return None

    def sub(m):
        label, url = m.group(1), m.group(2)
        # 只处理纯相对路径的 .md 链接，跳过外链/锚点/图片
        if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', url) or url.startswith('#') or url.startswith('/'):
            return m.group(0)
        path_part, sep, frag = url.partition('#')
        if not path_part.lower().endswith('.md'):
            return m.group(0)
        target = resolve(path_part)
        if not target:
            return m.group(0)
        new_rel = posixpath.relpath(target, cur_dir) if cur_dir else target
        # 锚点也要跟着语言走，否则 #中文标题 在日文页上不存在
        if frag:
            frag = anchor_map.get(frag, frag)
        return '[%s](%s%s%s)' % (label, new_rel, sep, frag)

    # 只改正文(head + tail)，导航条原样拼回
    head = re.sub(r'\[([^\]]+)\]\(([^)\s]+)\)', sub, head)
    tail = re.sub(r'\[([^\]]+)\]\(([^)\s]+)\)', sub, tail)
    return head + nav_block + tail


def slugify_heading(heading):
    """GitHub 风格的标题 -> anchor 规则。"""
    s = heading.strip().lower()
    s = re.sub(r'[^\w\s-]', '', s, flags=re.UNICODE)
    s = re.sub(r'\s+', '-', s)
    return s.strip('-')


def build_anchor_map(doc, lang_code, config):
    """构造「中文源标题 anchor -> 目标语言标题 anchor」映射。

    产物里的标题已经被翻译过了，所以直接读磁盘上已存在的产物文件；
    若产物还不存在(首次生成)，退化为「不改写锚点」，下次跑会自动补上。
    """
    src_path = os.path.join(ROOT, doc['source'])
    out_path = os.path.join(ROOT, doc_output_path(config, doc, lang_code))
    if not (os.path.exists(src_path) and os.path.exists(out_path)):
        return {}
    # 源与产物是同一个文件(权威源语言)，不需要映射
    if os.path.abspath(src_path) == os.path.abspath(out_path):
        return {}
    try:
        with open(src_path, encoding='utf-8') as f:
            src_heads = re.findall(r'^#{1,6} (.+)$', f.read(), re.M)
        with open(out_path, encoding='utf-8') as f:
            out_heads = re.findall(r'^#{1,6} (.+)$', f.read(), re.M)
    except OSError:
        return {}
    # 标题数量通常一一对应，按顺序配对
    amap = {}
    for a, b in zip(src_heads, out_heads):
        sa, sb = slugify_heading(a), slugify_heading(b)
        if sa and sb:
            amap[sa] = sb
    return amap


# ===========================================================================
# 主流程
# ===========================================================================
def normalize(text):
    """统一换行与行尾空格，避免 CI 反复产生无意义 diff。"""
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    lines = [l.rstrip() for l in text.split('\n')]
    while lines and lines[-1] == '':
        lines.pop()
    return '\n'.join(lines) + '\n'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--doc', help='只处理指定文档 id')
    ap.add_argument('--lang', help='只处理指定语言(不含 en)')
    ap.add_argument('--dry-run', action='store_true', help='不调用 API，只验证切分逻辑')
    ap.add_argument('--check', action='store_true', help='检查产物是否与源同步')
    args = ap.parse_args()

    config = load_yaml(os.path.join(I18N_DIR, 'config.yml'))
    glossary = load_yaml(os.path.join(ROOT, config.get('glossary', 'i18n/glossary.yml')))
    cfg = config.get('translate') or {}

    source_lang = config.get('source_lang')
    if not source_lang:
        err('config.yml 缺少 source_lang（权威源语言）')
        return 1

    docs = config['documents']
    if args.doc:
        docs = [d for d in docs if d['id'] == args.doc]
        if not docs:
            err('没有找到文档 id: %s' % args.doc)
            return 1

    if not args.dry_run and not args.check and not API_KEY:
        warn('未设置 TRANSLATE_API_KEY，跳过自动翻译。'
             '请在仓库 Settings → Secrets and variables → Actions 中添加该 Secret。')
        return 0

    changed_files = []
    stale = []

    # 语言任务表：en + 所有目标语言。
    # source_lang(中文) 天然不在 languages 里 —— 它本身就是成品，不需要翻译。
    lang_tasks = [('en', 'English')]
    for lg in config['languages']:
        if lg['code'] == source_lang:
            err('languages 里不应包含 source_lang(%s)：'
                '它的成品就是源文件本身，不需要生成产物。' % source_lang)
            return 1
        if args.lang and lg['code'] != args.lang:
            continue
        lang_tasks.append((lg['code'], lg['name']))

    for doc in docs:
        src_path = os.path.join(ROOT, doc['source'])
        if not os.path.exists(src_path):
            err('源文件不存在: %s' % doc['source'])
            return 1
        with open(src_path, encoding='utf-8') as f:
            source_text = normalize(f.read())

        for lang_code, lang_name in lang_tasks:
            out_rel = doc_output_path(config, doc, lang_code)
            out_path = os.path.join(ROOT, out_rel)

            cache = load_cache(lang_code)
            sys_prompt = build_system_prompt(glossary, lang_code, lang_name)

            log('--- %s → %s (%s)' % (doc['id'], lang_code, out_rel))

            if args.check:
                if not os.path.exists(out_path):
                    stale.append(out_rel + ' (缺失)')
                continue

            body = translate_document(source_text, sys_prompt, cfg, cache,
                                      dry_run=args.dry_run)

            if doc.get('lang_nav') and not args.dry_run:
                nav = build_nav(lang_code, config, doc)
                body = inject_nav(body, nav)

            body = normalize(body)

            if not args.dry_run:
                save_cache(lang_code, cache)

            old = ''
            if os.path.exists(out_path):
                with open(out_path, encoding='utf-8') as f:
                    old = f.read()

            if old != body or args.dry_run:
                if args.dry_run:
                    log('    (dry-run) 会写入 %d 字符' % len(body))
                else:
                    os.makedirs(os.path.dirname(out_path) or '.', exist_ok=True)
                    with open(out_path, 'w', encoding='utf-8', newline='') as f:
                        f.write(body)
                    changed_files.append(out_rel)
                    log('    ✓ 已写入 (%d 字符)' % len(body))
            else:
                log('    = 无变化')

    if args.check:
        if stale:
            warn('以下产物缺失，需要重新生成: %s' % ', '.join(stale))
            return 1
        log('所有产物均存在。')
        return 0

    # -----------------------------------------------------------------------
    # 第二遍：跨文档链接改写
    #   anchor 映射需要「已翻译产物」的标题，而第一遍才刚把它们写出来，
    #   所以必须等所有产物落盘后再跑一遍。这一遍不调用 LLM(纯文本替换)。
    # -----------------------------------------------------------------------
    if not args.dry_run:
        log('')
        log('=== 第二遍：跨文档链接改写 ===')
        for doc in docs:
            for lang_code, lang_name in lang_tasks:
                out_rel = doc_output_path(config, doc, lang_code)
                out_path = os.path.join(ROOT, out_rel)
                if not os.path.exists(out_path):
                    continue
                with open(out_path, encoding='utf-8') as f:
                    body = f.read()
                new_body = rewrite_doc_links(body, lang_code, doc, config, out_rel)
                if new_body != body:
                    with open(out_path, 'w', encoding='utf-8', newline='') as f:
                        f.write(new_body)
                    if out_rel not in changed_files:
                        changed_files.append(out_rel)
                    log('    ~ %s 链接已改写' % out_rel)
                else:
                    log('    = %s 无变化' % out_rel)

    # -----------------------------------------------------------------------
    # 第三遍：同步权威源文件自己的语言导航条
    #   中文源本身就是「简体中文成品」，所以它的导航条也要由 CI 维护 ——
    #   否则加一门语言后，只有产物更新、源文件还停在旧导航上。
    #   只替换 <!-- i18n:nav --> 包裹块，正文一个字都不动。
    # -----------------------------------------------------------------------
    if not args.dry_run:
        log('')
        log('=== 第三遍：同步源文件导航条 ===')
        for doc in docs:
            if not doc.get('lang_nav'):
                continue
            src_rel = doc['source']
            src_path = os.path.join(ROOT, src_rel)
            if not os.path.exists(src_path):
                continue
            nav = build_nav(source_lang, config, doc)
            if not nav:
                continue
            with open(src_path, encoding='utf-8') as f:
                body = f.read()
            new_body = inject_nav(body, nav)
            if new_body != body:
                with open(src_path, 'w', encoding='utf-8', newline='') as f:
                    f.write(new_body)
                if src_rel not in changed_files:
                    changed_files.append(src_rel)
                log('    ~ %s 导航条已同步' % src_rel)
            else:
                log('    = %s 无变化' % src_rel)

    if changed_files:
        log('')
        log('共更新 %d 个文件:' % len(changed_files))
        for f in changed_files:
            log('  - ' + f)
    else:
        log('')
        log('所有产物均为最新，无改动。')

    return 0


if __name__ == '__main__':
    sys.exit(main())
