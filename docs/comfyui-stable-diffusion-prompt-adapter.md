# ComfyUI / Stable Diffusion 提示词适配研究

日期：2026-06-03

本文记录啊拼对 ComfyUI、Stable Diffusion WebUI 和 diffusers 输出提示词的适配方案。当前目标是生成“可复制字段”，不是直接调用本地 ComfyUI、A1111 或云端 API。

## 一手资料结论

- ComfyUI 的 `CLIPTextEncode` 节点把文本提示词转换成后续采样节点可用的 `CONDITIONING`；它的核心输入是 `text` 和 `clip`。官方文档还记录了 embedding 的用法，例如 `embedding:model_name`。参考：[ComfyUI CLIPTextEncode](https://docs.comfy.org/built-in-nodes/ClipTextEncode)。
- ComfyUI 的 `KSampler` 同时接收 `positive` 和 `negative` conditioning，并暴露 `seed`、`steps`、`cfg`、`sampler_name`、`scheduler`、`latent_image` 和 `denoise` 等关键参数。参考：[ComfyUI KSampler](https://docs.comfy.org/built-in-nodes/sampling/ksampler)。
- ComfyUI 的 SDXL 专用文本编码器有 `text_g`、`text_l`、`width`、`height`、`target_width`、`target_height` 等字段，说明 SDXL 适配不能只按 SD 1.5 单 prompt 处理。参考：[ComfyUI CLIPTextEncodeSDXL](https://docs.comfy.org/built-in-nodes/ClipTextEncodeSdxl)。
- Flux 类模型在 ComfyUI 中会使用不同文本编码字段，例如 `clip_l` 和 `t5xxl`，更适合自然语言描述加结构化关键词组合。参考：[ComfyUI CLIPTextEncodeFlux](https://docs.comfy.org/built-in-nodes/ClipTextEncodeFlux)。
- ComfyUI workflow JSON 是工作流图结构，节点 ID、输入连接和字段取决于用户自己的工作流；在用户没有提供 workflow JSON 时，啊拼不应编造完整工作流。参考：[ComfyUI workflow JSON spec](https://docs.comfy.org/specs/workflow_json)。
- Stable Diffusion WebUI / A1111 支持正向 prompt、negative prompt、注意力权重、LoRA 标签等语法；官方 Wiki 明确 LoRA 使用 `<lora:filename:multiplier>`，且 LoRA 不应放进 negative prompt。参考：[AUTOMATIC1111 Features](https://github.com/AUTOMATIC1111/stable-diffusion-webui/wiki/Features)。
- diffusers 的 StableDiffusionPipeline 以 `prompt`、`negative_prompt`、`height`、`width`、`num_inference_steps`、`guidance_scale`、`generator` 等参数为核心。参考：[diffusers Stable Diffusion text-to-image](https://huggingface.co/docs/diffusers/main/api/pipelines/stable_diffusion/text2img)。
- diffusers 的 SDXL pipeline 支持 `prompt` / `prompt_2` 与 `negative_prompt` / `negative_prompt_2` 等双编码器字段，并支持 IP-Adapter 图像输入字段。参考：[diffusers Stable Diffusion XL](https://huggingface.co/docs/diffusers/main/api/pipelines/stable_diffusion/stable_diffusion_xl)。

## 啊拼输出策略

默认输出三组字段：

1. `ComfyUI 节点字段`
   - `正向 CLIP 文本编码（Positive CLIP Text Encode）`
   - `反向 CLIP 文本编码（Negative CLIP Text Encode）`
   - `KSampler 参数`
   - `可选节点`
2. `Stable Diffusion WebUI 字段`
   - `正向提示词（Prompt）`
   - `反向提示词（Negative prompt）`
   - `生成参数（Parameters）`
3. `diffusers 参数`
   - `提示词（prompt）`
   - `反向提示词（negative_prompt）`
   - `宽度（width）`
   - `高度（height）`
   - `推理步数（num_inference_steps）`
   - `引导强度（guidance_scale）`
   - `随机种子（seed）`

默认不输出完整 ComfyUI workflow JSON。只有当用户提供 workflow JSON 或明确给出节点 ID 时，后续才适合生成节点字段 patch。

## 适配规则

- 中文主输出使用中文字段名、中文占位符和中文提示词正文；英文提示词栏单独生成英文可执行版本，便于 SD 1.5、SDXL、SD3 和常见 LoRA/embedding 生态使用。
- 中文 Logo、中文海报标题、中文 UI 文案等必须保留原文，但应明确“文字内容为：...”。
- 负向提示词必须包含低质量、模糊、结构错误、多余肢体、文字乱码、水印、主体漂移等基础排除项。
- 人物或角色主体必须包含成年约束；负向提示词必须排除 `underage`、`child`、`teenager`、`juvenile`、`loli`、`young-looking`、`age ambiguity`。
- LoRA 标签只在正向提示词或可选节点中输出，不放进 negative prompt。
- 不编造 LoRA 名、embedding 名、ControlNet 类型、参考图、seed、尺寸、checkpoint、sampler 或 scheduler。
- SDXL / Flux / SD3 没有确定模型族时，不硬套单一最佳参数；用 `待补充` 保留。
- img2img / inpainting / ControlNet / IP-Adapter 必须明确参考图承担的控制作用、`denoise` 或 strength 的意图，以及哪些主体特征必须保持。

## 当前落地

- 内置优化目标：`builtin-comfyui-stable-diffusion`。
- 模板来源：`ComfyUI / Stable Diffusion`。
- 内置模板：
  - `builtin-sd-comfyui-node-fields`
  - `builtin-sd-webui-positive-negative`
  - `builtin-sd-img2img-controlnet`
  - `builtin-sd-lora-embedding`

## 后续可做

- 复制按钮细分：复制 ComfyUI 正向、复制 ComfyUI 负向、复制 WebUI 参数、复制 diffusers 参数。
- 支持导入用户 ComfyUI workflow JSON，然后按节点标题或 ID 生成 patch。
- 支持本地 ComfyUI API，但必须放在用户显式配置后，不自动扫描或连接本地服务。
- 为 SDXL、SD3、Flux 增加模型族专用输出模式。
