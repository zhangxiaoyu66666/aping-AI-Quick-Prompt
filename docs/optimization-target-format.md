# 啊拼 Optimization Target Format

啊拼 的优化目标包使用 JSON 文件，当前 schema 为：

```json
{
  "schema": "aipin.optimization_target.v1",
  "items": []
}
```

应用也支持直接导入单个目标对象或目标对象数组。导入后，目标会出现在“优化目标”管理页和右侧“已挂载优化目标”列表中，用户可以挂载多个目标并随时切换；导出时会统一写成带 `items` 的包格式，方便分享。

## 字段

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `id` | 否 | 稳定 ID。为空时会按标题生成。 |
| `title` | 是 | 优化目标显示名称。 |
| `description` | 否 | 简短说明，会用于提示和分享说明。 |
| `category` | 否 | 分类，默认 `用户目标`。 |
| `templateSource` | 否 | 右侧模板栏默认关联源，如 `ChatGPT-Shortcut`、`prompts.chat`、`SD-Anima-Prompt-Studio`、`AI编程`、`Skill体系`。 |
| `compatibility` | 否 | 底部兼容信息，如 `ChatGPT / Claude / Gemini / 本地模型`。 |
| `keywords` | 否 | 触发词或搜索词。 |
| `localPromptTemplate` | 是 | 本地草稿模板。支持变量替换。 |
| `modelInstruction` | 否 | 调用大模型继续优化时使用的目标规则。 |
| `englishTranslationRule` | 否 | 英文提示词同步时的结构保持规则。 |

## 可用变量

`localPromptTemplate` 支持 `{{变量名}}` 或 `{变量名}`：

- `{{userRequest}}`：用户输入内容
- `{{localPrompt}}`：系统本地结构化草稿
- `{{context}}`：额外上下文
- `{{contextSource}}`：上下文来源
- `{{primaryLanguage}}`：主输出语言
- `{{effectiveMode}}`：当前优化目标名称
- `{{targetTitle}}`：目标标题
- `{{targetDescription}}`：目标说明
- `{{targetCategory}}`：目标分类
- `{{templateSource}}`：关联模板源
- `{{compatibility}}`：兼容模型说明
- `{{keywords}}`：关键词列表

## 示例

```json
{
  "schema": "aipin.optimization_target.v1",
  "items": [
    {
      "id": "academic-humanize-cn",
      "title": "论文去AI味",
      "description": "把文本改写成自然、克制、有学术感的中文论文段落。",
      "category": "论文写作",
      "templateSource": "ChatGPT-Shortcut",
      "compatibility": "ChatGPT / Claude / Gemini / DeepSeek / Kimi / 本地模型",
      "keywords": ["/论文人话", "/去AI味论文", "/论文自然改写"],
      "localPromptTemplate": "你是一名中文论文写作润色助手。请把下面文本改写成自然、清楚、有学术感的论文段落，避免AI腔、机械排比和空话套话。只输出改写后的正文。\\n\\n文本：\\n{{userRequest}}",
      "modelInstruction": "生成一份可复制给目标模型使用的中文论文自然改写提示词。必须保留学术表达，但模拟真实学生或研究者的自然写作习惯；禁止机械列表、连续排比、空话套话和编造信息。",
      "englishTranslationRule": "Preserve the academic rewriting prompt as an executable instruction. Keep the anti-AI-tone rules and final text-only output rule."
    }
  ]
}
```
