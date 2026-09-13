# 文档多语言管理

本目录是整套多语言文档体系的配置与工具所在。**你只需要维护中文源文件**，其余全部由 GitHub Actions 自动生成。

---

## 一、你要记住的三件事

1. **只改 `docs/*.zh-CN.md`** —— 这是唯一的人工维护入口，它同时就是简体中文成品
2. **不要手动改产物** —— `README.md`、`README.<lang>.md`、`docs/CHANGELOG*.md`、`docs/CONTRIBUTING*.md` 都是 CI 生成的，下次同步会被覆盖
3. **英文是仓库门面** —— `README.md` 本身就是英文，GitHub 首页直接显示英文

> `zh-CN` 是**权威源语言**（`config.yml` 里的 `source_lang`），不参与翻译循环 ——
> 不需要「把中文翻译成中文」。所以你不会看到 `README.zh-CN.md`（仓库根）这种产物，
> 简体中文读者直接看 `docs/README.zh-CN.md` 源文件。

---

## 二、日常维护流程

### 改文档

```bash
# 1. 编辑中文源
vim docs/README.zh-CN.md

# 2. 提交推送
git add docs/README.zh-CN.md
git commit -m "docs: 补充常见问题"
git push
```

推送后 GitHub Actions 的 **Docs i18n** 工作流自动跑，把改动翻译成英文 + 繁中/日/韩，并 commit 回仓库（commit message 带 `[skip ci]`，不会循环触发）。

**一般 1~3 分钟**（取决于改了多少字，缓存命中的段落不重复翻译）。

> 导航条由 CI 维护：源文件里的 `<!-- i18n:nav -->` 区块也由工作流同步，
> 所以你**不用手动改导航条**。加语言后它会自动更新到所有文件。

### 加一门新语言

编辑 `i18n/config.yml`，在 `languages` 下加一段：

```yaml
  - code: de-DE
    name: Deutsch
    flag: "🇩🇪"
```

同时要做的两件事：
1. 在 `nav` 段补一行该语言的导航条模板（照抄现有格式，把自称换成该语言）
2. 在 `.github/workflows/i18n.yml` 最后的 `Verify outputs exist` 步骤里，把新的产物文件名加进检查列表

推送后 CI 会自动生成 `README.de-DE.md`、`docs/CHANGELOG.de-DE.md`、`docs/CONTRIBUTING.de-DE.md`。

> **不要把 `source_lang`（`zh-CN`）加进 `languages`** —— 脚本会直接报错退出。
> 它已经在 `nav` 段里配好了模板（因为每种语言的导航条都要能指向它），
> 但不需要作为「目标语言」参与翻译。

### 加一个新文档

在 `i18n/config.yml` 的 `documents` 下加一段：

```yaml
  - id: faq
    source: docs/FAQ.zh-CN.md
    outputs:
      en: docs/FAQ.md
      lang: docs/FAQ.{lang}.md
    lang_nav: false        # 只有 README 需要语言导航条
```

然后创建 `docs/FAQ.zh-CN.md` 并推送即可。

### 让某个术语不被翻错

编辑 `i18n/glossary.yml`：
- **必须原样保留**的词 → 加到 `keep_verbatim`（比如新的类名、新协议名）
- **希望统一译法**的词 → 加到 `preferred`，按语言给译法

---

## 三、首次启用（必做一次）

工作流需要一个 LLM API Key 才能工作。

1. 打开仓库 **Settings → Secrets and variables → Actions**
2. 点 **New repository secret**
   - Name: `TRANSLATE_API_KEY`
   - Secret: 你的 API Key
3. （可选）切到 **Variables** 标签，加两个变量覆盖默认值：

| Variable | 默认值 | 说明 |
| --- | --- | --- |
| `TRANSLATE_API_BASE` | `https://api.deepseek.com/v1` | 接口地址。任何兼容 OpenAI 格式的服务都可以 |
| `TRANSLATE_MODEL` | `deepseek-chat` | 模型名 |

**没配 Key 会怎样**：工作流不会失败，只在日志里给一条 warning 然后跳过。所以你可以先合并，之后再配。

4. 配好后到 **Actions → Docs i18n → Run workflow** 手动跑一次，把全部语言产物生成出来。

### 换服务商

只改 `TRANSLATE_API_BASE` 与 `TRANSLATE_MODEL` 即可，例如：

| 服务商 | `TRANSLATE_API_BASE` | `TRANSLATE_MODEL` |
| --- | --- | --- |
| DeepSeek | `https://api.deepseek.com/v1` | `deepseek-chat` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| 通义千问 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-plus` |
| 智谱 | `https://open.bigmodel.cn/api/paas/v4` | `glm-4-flash` |

---

## 四、本地预览

```bash
pip install pyyaml

# 不调 API，只验证 Markdown 切分是否正确（排查结构问题时用）
python i18n/translate.py --dry-run

# 真的翻译（需要 Key）
export TRANSLATE_API_KEY=sk-xxxx
python i18n/translate.py

# 只翻某个文档 / 某个语言
python i18n/translate.py --doc readme --lang ja-JP

# 检查产物是否齐备（CI 也跑这个做守门）
python i18n/translate.py --check
```

---

## 五、工作原理

```
docs/README.zh-CN.md          ← 你维护这个（中文，同时就是简体中文成品）
        │
        │  GitHub Actions: i18n.yml 触发
        ▼
   i18n/translate.py
        │
        ├─【第一遍】切分 Markdown → 只有「自然语言段落」送去翻译
        │   代码块 / 表格结构 / 链接 / 行内代码 原样透传
        ├─ 注入术语表约束（keep_verbatim + preferred）
        ├─ 段落级缓存（i18n/.cache/<lang>.json）→ 没改过的段落不重复翻译
        ├─ 注入语言导航条（README 顶部）
        │
        ├─【第二遍】等所有产物落盘后，统一改写跨文档链接与锚点
        │   （纯文本替换，不调用 API —— 因为锚点映射需要已翻译的标题）
        │   导航条整块跳过，不参与改写
        │
        ├─【第三遍】同步中文源自己的语言导航条
        │   （源文件也是成品，它的导航条同样由 CI 维护）
        ▼
README.md · README.zh-TW.md · README.ja-JP.md · README.ko-KR.md
docs/CHANGELOG*.md · docs/CONTRIBUTING*.md
        │
        └─ 自动 commit 回仓库
```

> **zh-CN 不在产物列表里**：`config.yml` 的 `source_lang: zh-CN` 声明了权威源语言，
> 它不参与翻译循环。简体中文的成品就是 `docs/README.zh-CN.md` 本身。

> **为什么分两遍？** 中文源里写 `[CHANGELOG](CHANGELOG.md)`（相对 `docs/`），
> 而锚点形如 `#添加新的语言翻译`。要把它改写成 `docs/CHANGELOG.ja-JP.md#新しい-ui-言語の追加`，
> 必须先知道「日文产物的标题长什么样」。第一遍才刚把日文产物写出来，
> 所以链接与锚点的改写只能放到第二遍。第二遍不调 LLM，成本可忽略。
>
> 导航条里的链接是「指向别语言」的，如果被当成普通跨文档链接一起改写，
> 就会变成指向自己、路径也会错乱 —— 所以第二遍会**先把导航条整块摘出来**，只改正文。

### 为什么不会翻坏 Markdown

翻译器不会把整篇 Markdown 丢给 LLM，而是先切成 segment：

| segment 类型 | 处理方式 |
| --- | --- |
| 代码围栏 ` ``` ` | **整块原样保留**，一个字节都不动 |
| 表格 | 逐单元格翻译，**行列数绝对不变**，分隔行不动 |
| 链接 / 图片 / 行内代码 | 先替换成占位符 `\x00N\x00`，翻完再还原 |
| HTML 块 | 只翻标签之间的文本，标签与属性不动 |
| 纯语言段落 | 送 LLM 翻译 |

代码块里的 `#Region`、表格的 `|---|`、徽章里的 URL 都不会被误翻。

代码围栏的闭合判定要求「**同类字符**且**长度不短于起始行**且后面只剩空白」，
所以目录树里缩进的 ` ``` ` 不会被误判成闭合标记 —— 这是最容易翻车的地方。

### 段落缓存

每个自然语言段落按其内容的 hash 缓存译文。改一段话只重翻那一段——不是重翻整个文件。这也意味着**修改少量文字时 Actions 跑得很快、花费很少**。

---

## 六、Release 说明的英文优先

GitHub Release 的正文（release notes）不走上面的翻译流程，而是**直接写中文 + 英文双语**，因为发布说明需要人工把关措辞。

建议格式：

```markdown
## What's Changed
...

## 中文说明
...
```

发布时按 `docs/CONTRIBUTING.zh-CN.md` 里的「发布流程」章节操作。仓库根目录的 `release-notes-v2.5-rc*.md` 是历史发布草稿，可参考其结构。

---

## 七、排查

| 现象 | 原因与处理 |
| --- | --- |
| Actions 跑完但没生成文件 | 没配 `TRANSLATE_API_KEY`，看日志里的 warning |
| 某语言产物没更新 | 看 `Show changes` 步骤的输出；可能是该段命中缓存且内容确实没变 |
| Markdown 结构被破坏 | 用 `--dry-run` 本地看切分结果；若某段被误判成 prose，检查是否有异常缩进的围栏 |
| 术语被翻错 | 把词加到 `i18n/glossary.yml` 的 `keep_verbatim` |
| 翻译质量整体不好 | 换更强的 `TRANSLATE_MODEL`，或调 `i18n/config.yml` 里的 `temperature` |
| 想强制重翻全部 | 删掉 `i18n/.cache/` 后推送，或手动触发 workflow |
