use std::io::{BufRead, Read, Write};
use std::path::PathBuf;
use std::sync::OnceLock;
use std::time::Instant;

use base64::{engine::general_purpose, Engine as _};
use image::{DynamicImage, GenericImageView, Rgba, RgbaImage};
use jieba_rs::{Jieba, TokenizeMode};
use ocr_rs::{Backend, DetOptions, OcrEngine, OcrEngineConfig, OcrResult_, RecOptions};
use serde::{Deserialize, Serialize};
use serde_json::json;

const DET_MODEL_FP16: &[u8] =
    include_bytes!("../../../assets/fire_eye/PP-OCRv5_mobile_det_fp16.mnn");
const REC_MODEL_FP16: &[u8] =
    include_bytes!("../../../assets/fire_eye/PP-OCRv5_mobile_rec_fp16.mnn");
const DET_MODEL: &[u8] = include_bytes!("../../../assets/fire_eye/PP-OCRv5_mobile_det.mnn");
const REC_MODEL: &[u8] = include_bytes!("../../../assets/fire_eye/PP-OCRv5_mobile_rec.mnn");
const CHARSET: &[u8] = include_bytes!("../../../assets/fire_eye/ppocr_keys_v5.dict");
const JSON_MARKER: &str = "__XIAXIA_FIRE_EYE_JSON__";
const DAEMON_ARG: &str = "--daemon";
const HEALTHCHECK_PNG_BASE64: &str = concat!(
    "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",
    "WQz6rWo2Z9FfNJ72JaqxTmhf9p7dHEWm9aC5yP6eQ5Q/MYlxmvfdvnZfvyLKnejG9Oz6zER19go1E/Uvohx194/2tug+1pVcZ22yMQmdE1svk3FN57brK9Ll",
    "MBLVY+WOdTQZa0XHlK0L4edJTMZWiXI7Geu0hipdHFEtd/PYrbdIF4fXvT6Ku4uryl20D0Tz6vkYRJa/bkwVjc9+EeHvX8liUpPYdttM38e62Lvnd+MU3T2r",
    "WtOaeFe3jSxahN50Yrp/pcp+47LyTYv+he7sfvbXT6wujmjTycak/OutKKdRDqPHrCwG7Vvaezanj+hLVGON2BiifGss9p7dHEW1Mq0H279vJ/9b2/lcTGKc",
    "5j3bmJW203+1axJbJ8qd6Mak7e6uz0mdfVTN+MeVn0cR9S+iHHXzHu1t0X2sKNedro2PwZI28pzdLyfjms5t11e0dqIcTuqxoq995DqajLVi5yH6BzbsnFi7",
    "Y5vkdjLWaQ1VujiiWu7msVtvkd29aBJ3F5d/vRXtA7aP6DCR1VeWv25MUU0IPy5pq69b/dXRLCY1iW13TqfvY13s3fOTsXX39HNi6b3e1W0jixahN50Yu2jk",
    "GwL5NRFl/y5A9k/RevZ+0pe+AUkB6TiiQrDFJn1qO7mf3XDsws7GpKJ+VJTTKIfRY1YWgy5ayZsuHhmL/fbSt5v2JfR+0Zt3RnNg8y3kzzrv9td7JrXiN7DV",
    "erB92RzK+LSfKBeTGKd5tzXr51nfBOXSOZnE1vF9qG5Mz6zPK3X2jJrZnUcRPSaiHE32tug+VpbrStdG44ho3u1+ORnXI+Z2de1kOdytx8od62gy1oqdC7n0",
    "ftU8icnYdnM7Geu0hiqTWu7msVtvkd29aBJ3F5e2iUT7gK8vPRT6mP1hMcvfpCZk3NpGcyZsrdrHM1lMahLb7pzaPnbex7rYu+cnY+vuWdWaH/e7iVfQA0SL",
    "0LsyMXYxRZcUghTLqup++i2EXJ4UnX+9vfybdDUmkfUjopxGOYwes7IY/JumveSN1m4EatqXsN+KyrWyyKp50stvol2bqFaqNjoPcnm2VvwV/X9gqao/uXyM",
    "V/JezbNcvmZ3Y+tk896NqYr70fXZjVmunTqr9pBpzVT50LbWbt5397bsPirLdaVro7FEdJ3696DdcYnp3Fbt5PJrJ8thdx+5fD1mqrqZriPRxejHWtF5lzb2",
    "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",
    "rkJggg==",
);

static JIEBA: OnceLock<Jieba> = OnceLock::new();

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct FireEyeBackendCandidate {
    backend: String,
    model_variant: String,
    benchmark_ms: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct FireEyeOcrCapabilities {
    provider_id: String,
    available: bool,
    error: Option<String>,
    language_tags: Vec<String>,
    language_names: Vec<String>,
    max_image_dimension: Option<u32>,
    recognizer_language_tag: Option<String>,
    recognizer_language_name: Option<String>,
    selected_backend: Option<String>,
    selected_model_variant: Option<String>,
    benchmark_ms: Option<u64>,
    backend_candidates: Vec<FireEyeBackendCandidate>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrPoint {
    x: f32,
    y: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrBoundingBox {
    x: f32,
    y: f32,
    width: f32,
    height: f32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrCropRect {
    x: u32,
    y: u32,
    width: u32,
    height: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrInput {
    image_data_url: Option<String>,
    image_path: Option<String>,
    crop_rect: Option<NativeOcrCropRect>,
    contrast_enhanced: Option<bool>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrWord {
    id: String,
    text: String,
    confidence: Option<f32>,
    bbox: NativeOcrBoundingBox,
    polygon: Vec<NativeOcrPoint>,
    line_id: Option<String>,
    order: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrLine {
    id: String,
    text: String,
    confidence: Option<f32>,
    bbox: NativeOcrBoundingBox,
    polygon: Vec<NativeOcrPoint>,
    word_ids: Vec<String>,
    order: u32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct NativeOcrResult {
    provider: String,
    text: String,
    confidence: Option<f32>,
    width: u32,
    height: u32,
    words: Vec<NativeOcrWord>,
    lines: Vec<NativeOcrLine>,
    meta: serde_json::Value,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct WorkerRequest {
    command: String,
    input: Option<NativeOcrInput>,
    languages: Option<Vec<String>>,
    backend: Option<String>,
    model_variant: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct WorkerResponse {
    ok: bool,
    capabilities: Option<FireEyeOcrCapabilities>,
    candidate: Option<FireEyeBackendCandidate>,
    result: Option<NativeOcrResult>,
    error: Option<String>,
}

struct LoadedImage {
    image: DynamicImage,
    source_width: u32,
    source_height: u32,
    input_kind: &'static str,
    crop_rect: Option<NativeOcrCropRect>,
    contrast_enhanced: bool,
}

#[derive(Clone, Copy)]
struct Candidate {
    backend: Backend,
    backend_name: &'static str,
    model_variant: &'static str,
    det_model: &'static [u8],
    rec_model: &'static [u8],
}

struct RuntimeSummary {
    backend: &'static str,
    model_variant: &'static str,
    benchmark_ms: u64,
}

struct Runtime {
    engine: OcrEngine,
    summary: RuntimeSummary,
}

struct Scheduler {
    runtimes: Vec<Runtime>,
}

#[derive(Default)]
struct WorkerState {
    scheduler: Option<Result<Scheduler, String>>,
}

fn response_error(error: impl Into<String>) -> WorkerResponse {
    WorkerResponse {
        ok: false,
        capabilities: None,
        candidate: None,
        result: None,
        error: Some(error.into()),
    }
}

fn jieba() -> &'static Jieba {
    JIEBA.get_or_init(Jieba::new)
}

fn buffer_looks_like_text_dictionary(bytes: &[u8]) -> bool {
    let prefix_len = bytes.len().min(512);
    let prefix = &bytes[..prefix_len];
    let newline_count = prefix.iter().filter(|byte| **byte == b'\n').count();
    std::str::from_utf8(prefix).is_ok() && newline_count >= 8
}

fn validate_model_asset(label: &str, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() < 1024 * 1024 {
        return Err(format!(
            "{label} 不是有效的 MNN 模型：文件太小（{} 字节）。",
            bytes.len()
        ));
    }
    if buffer_looks_like_text_dictionary(bytes) {
        return Err(format!("{label} 指向了文本字典内容，不是 MNN 模型。"));
    }
    Ok(())
}

fn validate_charset_asset(bytes: &[u8]) -> Result<(), String> {
    let content = std::str::from_utf8(bytes)
        .map_err(|error| format!("火眼金睛字典不是 UTF-8 文本：{error}"))?;
    let char_count = content.chars().filter(|ch| !ch.is_whitespace()).count();
    if char_count < 100 {
        return Err(format!(
            "火眼金睛字典字符数量异常：只读到 {char_count} 个字符。"
        ));
    }
    Ok(())
}

fn validate_assets() -> Result<(), String> {
    validate_model_asset("PP-OCRv5_mobile_det.mnn", DET_MODEL)?;
    validate_model_asset("PP-OCRv5_mobile_rec.mnn", REC_MODEL)?;
    validate_model_asset("PP-OCRv5_mobile_det_fp16.mnn", DET_MODEL_FP16)?;
    validate_model_asset("PP-OCRv5_mobile_rec_fp16.mnn", REC_MODEL_FP16)?;
    validate_charset_asset(CHARSET)
}

fn backend_rank(backend: &str) -> u8 {
    match backend {
        "opencl" => 0,
        "vulkan" => 1,
        _ => 2,
    }
}

fn candidate_rank(summary: &RuntimeSummary) -> (u8, u8, u64) {
    let known_bad_precision = summary.backend == "vulkan" && summary.model_variant.contains("fp16");
    let precision_rank = if summary.model_variant.contains("fp16") {
        0
    } else {
        1
    };
    (
        if known_bad_precision { 1 } else { 0 },
        backend_rank(summary.backend).saturating_mul(2) + precision_rank,
        summary.benchmark_ms,
    )
}

fn candidate_priority(candidate: &Candidate) -> (u8, u8) {
    let summary = RuntimeSummary {
        backend: candidate.backend_name,
        model_variant: candidate.model_variant,
        benchmark_ms: 0,
    };
    let (quality_rank, backend_precision_rank, _) = candidate_rank(&summary);
    (quality_rank, backend_precision_rank)
}

fn candidates_by_preference() -> Vec<Candidate> {
    let mut candidates = candidates();
    candidates.sort_by_key(candidate_priority);
    candidates
}

fn preload_candidates_by_preference() -> Result<Vec<Candidate>, String> {
    let backend_filter = std::env::var("XIAXIA_FIRE_EYE_PRELOAD_BACKEND")
        .ok()
        .map(|value| value.to_ascii_lowercase());
    let model_filter = std::env::var("XIAXIA_FIRE_EYE_PRELOAD_MODEL")
        .ok()
        .map(|value| value.to_ascii_lowercase());
    let candidates = candidates_by_preference()
        .into_iter()
        .filter(|candidate| {
            backend_filter
                .as_deref()
                .map_or(true, |backend| candidate.backend_name == backend)
                && model_filter
                    .as_deref()
                    .map_or(true, |model| candidate.model_variant == model)
        })
        .collect::<Vec<_>>();

    if candidates.is_empty() {
        return Err(format!(
            "没有匹配的火眼金睛预加载候选 backend={:?}, model={:?}",
            backend_filter, model_filter
        ));
    }

    Ok(candidates)
}

fn result_text_and_confidence(result: &[OcrResult_]) -> (String, Option<f32>) {
    let mut total_confidence = 0.0f32;
    let mut confidence_count = 0u32;
    let text = result
        .iter()
        .filter_map(|line| {
            let text = line.text.trim();
            if text.is_empty() {
                None
            } else {
                total_confidence += line.confidence;
                confidence_count += 1;
                Some(text.to_string())
            }
        })
        .collect::<Vec<_>>()
        .join("\n");
    let confidence = if confidence_count == 0 {
        None
    } else {
        Some(total_confidence / confidence_count as f32)
    };
    (text, confidence)
}

fn validate_runtime_result(result: &[OcrResult_]) -> Result<(), String> {
    let (text, confidence) = result_text_and_confidence(result);
    if text.trim().is_empty() {
        return Err("返回了空文本".to_string());
    }
    if confidence.unwrap_or(0.0) < 0.20 {
        return Err(format!("平均置信度过低 ({:.3})", confidence.unwrap_or(0.0)));
    }
    Ok(())
}

fn candidates() -> Vec<Candidate> {
    let mut candidates = Vec::new();
    if cfg!(feature = "gpu") && std::env::var_os("XIAXIA_FIRE_EYE_DISABLE_GPU").is_none() {
        candidates.extend([
            Candidate {
                backend: Backend::Vulkan,
                backend_name: "vulkan",
                model_variant: "ppocr-v5-fp16",
                det_model: DET_MODEL_FP16,
                rec_model: REC_MODEL_FP16,
            },
            Candidate {
                backend: Backend::Vulkan,
                backend_name: "vulkan",
                model_variant: "ppocr-v5",
                det_model: DET_MODEL,
                rec_model: REC_MODEL,
            },
            Candidate {
                backend: Backend::OpenCL,
                backend_name: "opencl",
                model_variant: "ppocr-v5-fp16",
                det_model: DET_MODEL_FP16,
                rec_model: REC_MODEL_FP16,
            },
            Candidate {
                backend: Backend::OpenCL,
                backend_name: "opencl",
                model_variant: "ppocr-v5",
                det_model: DET_MODEL,
                rec_model: REC_MODEL,
            },
        ]);
    }
    candidates.extend([
        Candidate {
            backend: Backend::CPU,
            backend_name: "cpu",
            model_variant: "ppocr-v5-fp16",
            det_model: DET_MODEL_FP16,
            rec_model: REC_MODEL_FP16,
        },
        Candidate {
            backend: Backend::CPU,
            backend_name: "cpu",
            model_variant: "ppocr-v5",
            det_model: DET_MODEL,
            rec_model: REC_MODEL,
        },
    ]);
    candidates
}

fn resolve_candidate(backend_name: &str, model_variant: &str) -> Result<Candidate, String> {
    candidates()
        .into_iter()
        .find(|candidate| {
            candidate.backend_name == backend_name && candidate.model_variant == model_variant
        })
        .ok_or_else(|| format!("未知的火眼金睛候选：{backend_name}/{model_variant}"))
}

fn create_runtime(candidate: Candidate) -> Result<Runtime, String> {
    validate_model_asset("火眼金睛检测模型", candidate.det_model)?;
    validate_model_asset("火眼金睛识别模型", candidate.rec_model)?;
    validate_charset_asset(CHARSET)?;

    let config = OcrEngineConfig::fast()
        .with_backend(candidate.backend)
        .with_threads(4)
        .with_parallel(false)
        .with_min_result_confidence(0.35)
        .with_det_options(DetOptions::fast())
        .with_rec_options(RecOptions::new().with_batch_size(16));

    let engine = OcrEngine::from_bytes(
        candidate.det_model,
        candidate.rec_model,
        CHARSET,
        Some(config),
    )
    .map_err(|error| {
        format!(
            "{}/{}: {error}",
            candidate.backend_name, candidate.model_variant
        )
    })?;

    Ok(Runtime {
        engine,
        summary: RuntimeSummary {
            backend: candidate.backend_name,
            model_variant: candidate.model_variant,
            benchmark_ms: 0,
        },
    })
}

fn build_benchmark_image() -> DynamicImage {
    let mut benchmark = RgbaImage::from_pixel(960, 384, Rgba([255, 255, 255, 255]));
    let rows = [(52, 94), (132, 176), (214, 258), (294, 338)];
    for (row_index, (top, bottom)) in rows.into_iter().enumerate() {
        for x in 44..916 {
            let pattern =
                ((x / (9 + row_index as u32)) + row_index as u32) % (3 + row_index as u32);
            for y in top..bottom {
                if pattern != 0 && y % (9 + row_index as u32) < 7 {
                    benchmark.put_pixel(x, y, Rgba([20, 20, 20, 255]));
                }
            }
        }
    }
    DynamicImage::ImageRgba8(benchmark)
}

fn build_healthcheck_image() -> DynamicImage {
    let bytes = general_purpose::STANDARD
        .decode(HEALTHCHECK_PNG_BASE64)
        .expect("embedded Fire Eye healthcheck PNG base64 is invalid");
    image::load_from_memory(&bytes).expect("embedded Fire Eye healthcheck PNG is invalid")
}

fn warm_candidate(runtime: &Runtime, image: &DynamicImage) -> Result<(), String> {
    runtime.engine.recognize(image).map_err(|error| {
        format!(
            "预热火眼金睛 OCR 运行时失败 ({}/{}): {error}",
            runtime.summary.backend, runtime.summary.model_variant
        )
    })?;
    Ok(())
}

fn health_candidate(candidate: Candidate) -> Result<FireEyeBackendCandidate, String> {
    let health_image = build_healthcheck_image();
    let runtime = create_runtime(candidate)?;
    let started = Instant::now();
    let result = runtime.engine.recognize(&health_image).map_err(|error| {
        format!(
            "健康探测火眼金睛失败 ({}/{}): {error}",
            runtime.summary.backend, runtime.summary.model_variant
        )
    })?;
    validate_runtime_result(&result).map_err(|error| {
        format!(
            "健康探测火眼金睛结果不可用 ({}/{}): {error}",
            runtime.summary.backend, runtime.summary.model_variant
        )
    })?;
    Ok(FireEyeBackendCandidate {
        backend: runtime.summary.backend.to_string(),
        model_variant: runtime.summary.model_variant.to_string(),
        benchmark_ms: started.elapsed().as_millis() as u64,
    })
}

fn benchmark_candidate(runtime: &Runtime, image: &DynamicImage) -> Result<u64, String> {
    let started = Instant::now();
    runtime.engine.recognize(image).map_err(|error| {
        format!(
            "基准测试火眼金睛失败 ({}/{}): {error}",
            runtime.summary.backend, runtime.summary.model_variant
        )
    })?;
    Ok(started.elapsed().as_millis() as u64)
}

fn probe_candidate(candidate: Candidate) -> Result<FireEyeBackendCandidate, String> {
    let benchmark_image = build_benchmark_image();
    let mut runtime = create_runtime(candidate)?;
    warm_candidate(&runtime, &benchmark_image)?;
    runtime.summary.benchmark_ms = benchmark_candidate(&runtime, &benchmark_image)?;
    Ok(FireEyeBackendCandidate {
        backend: runtime.summary.backend.to_string(),
        model_variant: runtime.summary.model_variant.to_string(),
        benchmark_ms: runtime.summary.benchmark_ms,
    })
}

fn build_scheduler() -> Result<Scheduler, String> {
    validate_assets()?;
    let warmup_image = build_benchmark_image();
    let mut runtimes = Vec::new();
    let mut errors = Vec::new();

    for candidate in candidates_by_preference() {
        match create_runtime(candidate) {
            Ok(mut runtime) => {
                if let Err(error) = warm_candidate(&runtime, &warmup_image) {
                    errors.push(error);
                    continue;
                }
                runtime.summary.benchmark_ms = 0;
                runtimes.push(runtime);
            }
            Err(error) => errors.push(error),
        }
    }

    if runtimes.is_empty() {
        return Err(format!("初始化火眼金睛失败: {}", errors.join(" | ")));
    }

    runtimes.sort_by_key(|runtime| candidate_rank(&runtime.summary));
    Ok(Scheduler { runtimes })
}

fn build_preferred_scheduler() -> Result<Scheduler, String> {
    validate_assets()?;
    let warmup_image = build_healthcheck_image();
    let mut errors = Vec::new();

    for candidate in preload_candidates_by_preference()? {
        match create_runtime(candidate) {
            Ok(runtime) => {
                if let Err(error) = warm_candidate(&runtime, &warmup_image) {
                    errors.push(error);
                    continue;
                }
                return Ok(Scheduler {
                    runtimes: vec![runtime],
                });
            }
            Err(error) => errors.push(error),
        }
    }

    Err(format!("初始化火眼金睛失败: {}", errors.join(" | ")))
}

fn native_result_is_usable(result: &NativeOcrResult) -> Result<(), String> {
    if result.text.trim().is_empty() {
        return Err("返回了空文本".to_string());
    }
    if result.confidence.unwrap_or(0.0) < 0.20 {
        return Err(format!(
            "平均置信度过低 ({:.3})",
            result.confidence.unwrap_or(0.0)
        ));
    }
    Ok(())
}

fn recognize_with_candidate_fallback(
    input: &NativeOcrInput,
    requested_languages: &[String],
) -> Result<NativeOcrResult, String> {
    let mut errors = Vec::new();
    let mut first_empty_result = None;

    for candidate in candidates_by_preference() {
        let candidate_label = format!("{}/{}", candidate.backend_name, candidate.model_variant);
        match create_runtime(candidate).and_then(|runtime| {
            let scheduler = Scheduler {
                runtimes: vec![runtime],
            };
            recognize_with_scheduler(input, requested_languages, &scheduler)
        }) {
            Ok(result) => match native_result_is_usable(&result) {
                Ok(()) => return Ok(result),
                Err(error) => {
                    errors.push(format!("{candidate_label}: {error}"));
                    if result.text.trim().is_empty() && first_empty_result.is_none() {
                        first_empty_result = Some(result);
                    }
                }
            },
            Err(error) => errors.push(format!("{candidate_label}: {error}")),
        }
    }

    if let Some(result) = first_empty_result {
        return Ok(result);
    }
    Err(format!(
        "执行火眼金睛 OCR 失败，所有候选后端均不可用: {}",
        errors.join(" | ")
    ))
}

impl WorkerState {
    fn scheduler(&mut self) -> Result<&Scheduler, String> {
        if self.scheduler.is_none() {
            self.scheduler = Some(build_preferred_scheduler());
        }

        match self.scheduler.as_ref().expect("scheduler initialized") {
            Ok(scheduler) => Ok(scheduler),
            Err(error) => Err(error.clone()),
        }
    }
}

impl Scheduler {
    fn preferred_runtime(&self) -> Option<&Runtime> {
        self.runtimes.first()
    }

    fn candidate_summaries(&self) -> Vec<FireEyeBackendCandidate> {
        self.runtimes
            .iter()
            .map(|runtime| FireEyeBackendCandidate {
                backend: runtime.summary.backend.to_string(),
                model_variant: runtime.summary.model_variant.to_string(),
                benchmark_ms: runtime.summary.benchmark_ms,
            })
            .collect()
    }

    fn recognize<'a>(
        &'a self,
        image: &DynamicImage,
    ) -> Result<(&'a Runtime, Vec<OcrResult_>), String> {
        let mut errors = Vec::new();
        let mut first_empty_result = None;
        for runtime in &self.runtimes {
            match runtime.engine.recognize(image) {
                Ok(result) => match validate_runtime_result(&result) {
                    Ok(()) => return Ok((runtime, result)),
                    Err(error) => {
                        errors.push(format!(
                            "{}/{}: {error}",
                            runtime.summary.backend, runtime.summary.model_variant
                        ));
                        let (text, _) = result_text_and_confidence(&result);
                        if text.trim().is_empty() && first_empty_result.is_none() {
                            first_empty_result = Some((runtime, result));
                        }
                    }
                },
                Err(error) => errors.push(format!(
                    "{}/{}: {error}",
                    runtime.summary.backend, runtime.summary.model_variant
                )),
            }
        }
        if let Some(empty_result) = first_empty_result {
            return Ok(empty_result);
        }
        Err(format!("执行火眼金睛 OCR 失败: {}", errors.join(" | ")))
    }
}

fn decode_image_data_url(data_url: &str) -> Result<Vec<u8>, String> {
    let payload = data_url
        .split_once("base64,")
        .map(|(_, value)| value)
        .ok_or_else(|| "OCR 图片不是合法的 base64 data URL".to_string())?;
    general_purpose::STANDARD
        .decode(payload.trim())
        .map_err(|error| format!("OCR 图片解码失败: {error}"))
}

fn clamp_crop_rect(
    crop: &NativeOcrCropRect,
    width: u32,
    height: u32,
) -> Option<(u32, u32, u32, u32)> {
    let x = crop.x.min(width.saturating_sub(1));
    let y = crop.y.min(height.saturating_sub(1));
    let available_width = width.saturating_sub(x);
    let available_height = height.saturating_sub(y);
    let crop_width = crop.width.max(1).min(available_width);
    let crop_height = crop.height.max(1).min(available_height);
    if crop_width == 0 || crop_height == 0 {
        None
    } else {
        Some((x, y, crop_width, crop_height))
    }
}

fn find_histogram_percentile(histogram: &[u32; 256], target_count: u32) -> u8 {
    let mut accumulated = 0u32;
    for (index, count) in histogram.iter().enumerate() {
        accumulated += *count;
        if accumulated >= target_count {
            return index as u8;
        }
    }
    255
}

fn apply_contrast_enhancement(image: DynamicImage) -> DynamicImage {
    let mut rgba = image.to_rgba8();
    let pixel_count = rgba.width().saturating_mul(rgba.height()) as usize;
    if pixel_count == 0 {
        return DynamicImage::ImageRgba8(rgba);
    }

    let mut histogram = [0u32; 256];
    let mut luminance_values = Vec::with_capacity(pixel_count);
    for pixel in rgba.pixels() {
        let luminance =
            ((pixel[0] as f32 * 0.299) + (pixel[1] as f32 * 0.587) + (pixel[2] as f32 * 0.114))
                .round() as u8;
        histogram[luminance as usize] += 1;
        luminance_values.push(luminance);
    }

    let total_pixels = pixel_count.max(1) as u32;
    let clip_pixels = ((total_pixels as f32) * 0.01).floor().max(1.0) as u32;
    let low = find_histogram_percentile(&histogram, clip_pixels);
    let high =
        find_histogram_percentile(&histogram, total_pixels.saturating_sub(clip_pixels).max(1));
    let dynamic_range = ((high as i32 - low as i32).max(32)) as f32;

    for (pixel, luminance) in rgba.pixels_mut().zip(luminance_values.into_iter()) {
        let normalized = (((luminance as f32) - low as f32) / dynamic_range).clamp(0.0, 1.0);
        let gamma_adjusted = normalized.powf(0.9);
        let contrasted = ((gamma_adjusted - 0.5) * 1.55 + 0.5).clamp(0.0, 1.0);
        let value = (contrasted * 255.0).round() as u8;
        pixel[0] = value;
        pixel[1] = value;
        pixel[2] = value;
    }

    DynamicImage::ImageRgba8(rgba)
}

fn load_image(input: &NativeOcrInput) -> Result<LoadedImage, String> {
    let contrast_enhanced = input.contrast_enhanced.unwrap_or(false);
    let (mut image, input_kind) = if let Some(image_path) = input.image_path.as_deref() {
        let path = PathBuf::from(image_path);
        let image = image::open(&path)
            .map_err(|error| format!("读取 OCR 源截图失败 ({}): {error}", path.display()))?;
        (image, "filePath")
    } else if let Some(image_data_url) = input.image_data_url.as_deref() {
        let bytes = decode_image_data_url(image_data_url)?;
        let image = image::load_from_memory(&bytes)
            .map_err(|error| format!("解码 OCR 图片失败: {error}"))?;
        (image, "dataUrl")
    } else {
        return Err("OCR 输入为空，既没有图片路径也没有图片数据。".to_string());
    };

    let (source_width, source_height) = image.dimensions();
    let crop_rect = input.crop_rect.clone();
    if let Some(crop) = crop_rect.as_ref() {
        if let Some((x, y, width, height)) = clamp_crop_rect(crop, source_width, source_height) {
            image = image.crop_imm(x, y, width, height);
        }
    }
    if contrast_enhanced {
        image = apply_contrast_enhancement(image);
    }

    Ok(LoadedImage {
        image,
        source_width,
        source_height,
        input_kind,
        crop_rect,
        contrast_enhanced,
    })
}

fn polygon_from_bbox(bbox: &NativeOcrBoundingBox) -> Vec<NativeOcrPoint> {
    vec![
        NativeOcrPoint {
            x: bbox.x,
            y: bbox.y,
        },
        NativeOcrPoint {
            x: bbox.x + bbox.width,
            y: bbox.y,
        },
        NativeOcrPoint {
            x: bbox.x + bbox.width,
            y: bbox.y + bbox.height,
        },
        NativeOcrPoint {
            x: bbox.x,
            y: bbox.y + bbox.height,
        },
    ]
}

fn union_bboxes(bboxes: &[NativeOcrBoundingBox]) -> NativeOcrBoundingBox {
    if bboxes.is_empty() {
        return NativeOcrBoundingBox {
            x: 0.0,
            y: 0.0,
            width: 0.0,
            height: 0.0,
        };
    }

    let mut min_x = f32::MAX;
    let mut min_y = f32::MAX;
    let mut max_x = 0.0f32;
    let mut max_y = 0.0f32;
    for bbox in bboxes {
        min_x = min_x.min(bbox.x);
        min_y = min_y.min(bbox.y);
        max_x = max_x.max(bbox.x + bbox.width);
        max_y = max_y.max(bbox.y + bbox.height);
    }

    NativeOcrBoundingBox {
        x: min_x,
        y: min_y,
        width: (max_x - min_x).max(0.0),
        height: (max_y - min_y).max(0.0),
    }
}

fn contains_cjk(text: &str) -> bool {
    text.chars().any(|ch| {
        matches!(
            ch as u32,
            0x3040..=0x30ff
                | 0x3400..=0x4dbf
                | 0x4e00..=0x9fff
                | 0xf900..=0xfaff
                | 0x20000..=0x2ebef
        )
    })
}

fn bbox_from_polygon(points: &[NativeOcrPoint]) -> NativeOcrBoundingBox {
    if points.is_empty() {
        return NativeOcrBoundingBox {
            x: 0.0,
            y: 0.0,
            width: 0.0,
            height: 0.0,
        };
    }
    let min_x = points.iter().map(|point| point.x).fold(f32::MAX, f32::min);
    let min_y = points.iter().map(|point| point.y).fold(f32::MAX, f32::min);
    let max_x = points.iter().map(|point| point.x).fold(0.0, f32::max);
    let max_y = points.iter().map(|point| point.y).fold(0.0, f32::max);
    NativeOcrBoundingBox {
        x: min_x.max(0.0),
        y: min_y.max(0.0),
        width: (max_x - min_x).max(0.0),
        height: (max_y - min_y).max(0.0),
    }
}

fn polygon_from_result(result: &OcrResult_) -> Vec<NativeOcrPoint> {
    if let Some(points) = result.bbox.points {
        return points
            .into_iter()
            .map(|point| NativeOcrPoint {
                x: point.x.max(0.0),
                y: point.y.max(0.0),
            })
            .collect();
    }

    let rect = result.bbox.rect;
    let left = rect.left().max(0) as f32;
    let top = rect.top().max(0) as f32;
    let right = left + rect.width().max(1) as f32;
    let bottom = top + rect.height().max(1) as f32;
    vec![
        NativeOcrPoint { x: left, y: top },
        NativeOcrPoint { x: right, y: top },
        NativeOcrPoint {
            x: right,
            y: bottom,
        },
        NativeOcrPoint { x: left, y: bottom },
    ]
}

fn average_confidence<I>(values: I) -> Option<f32>
where
    I: IntoIterator<Item = Option<f32>>,
{
    let mut total = 0.0f32;
    let mut count = 0u32;
    for value in values {
        if let Some(confidence) = value {
            total += confidence;
            count += 1;
        }
    }
    if count == 0 {
        None
    } else {
        Some(total / count as f32)
    }
}

fn create_word(
    id: String,
    text: String,
    confidence: Option<f32>,
    bbox: NativeOcrBoundingBox,
    line_id: &str,
    order: u32,
) -> NativeOcrWord {
    let polygon = polygon_from_bbox(&bbox);
    NativeOcrWord {
        id,
        text,
        confidence,
        bbox,
        polygon,
        line_id: Some(line_id.to_string()),
        order,
    }
}

fn build_line_words(
    line_id: &str,
    line_text: &str,
    line_bbox: &NativeOcrBoundingBox,
    fallback_confidence: Option<f32>,
    word_order: &mut u32,
) -> (Vec<NativeOcrWord>, bool) {
    if contains_cjk(line_text) {
        let tokens = jieba()
            .tokenize(line_text, TokenizeMode::Default, false)
            .into_iter()
            .filter_map(|token| {
                let text = token.word.trim().to_string();
                if text.is_empty() {
                    None
                } else {
                    Some((text, token.start as f32, token.end as f32))
                }
            })
            .collect::<Vec<_>>();

        if !tokens.is_empty() {
            let total_chars = line_text.chars().count().max(1) as f32;
            let max_x = line_bbox.x + line_bbox.width.max(1.0);
            let token_count = tokens.len();
            let mut words = Vec::with_capacity(token_count);
            for (index, (text, start, end)) in tokens.into_iter().enumerate() {
                let x = line_bbox.x + line_bbox.width * (start / total_chars);
                let next_x = if index + 1 == token_count {
                    max_x
                } else {
                    line_bbox.x + line_bbox.width * (end / total_chars)
                };
                let bbox = NativeOcrBoundingBox {
                    x: x.max(0.0),
                    y: line_bbox.y.max(0.0),
                    width: (next_x - x).max(1.0),
                    height: line_bbox.height.max(1.0),
                };
                words.push(create_word(
                    format!("{line_id}-jieba-{index}"),
                    text,
                    fallback_confidence,
                    bbox,
                    line_id,
                    *word_order,
                ));
                *word_order += 1;
            }
            return (words, true);
        }
    }

    let order = *word_order;
    *word_order += 1;
    (
        vec![create_word(
            format!("{line_id}-word-0"),
            line_text.to_string(),
            fallback_confidence,
            line_bbox.clone(),
            line_id,
            order,
        )],
        false,
    )
}

fn recognize_with_scheduler(
    input: &NativeOcrInput,
    requested_languages: &[String],
    scheduler: &Scheduler,
) -> Result<NativeOcrResult, String> {
    let load_started = Instant::now();
    let loaded = load_image(input)?;
    let load_ms = load_started.elapsed().as_millis() as u64;
    let (result_width, result_height) = loaded.image.dimensions();

    let recognize_started = Instant::now();
    let (runtime, mut result) = scheduler.recognize(&loaded.image)?;
    let recognize_ms = recognize_started.elapsed().as_millis() as u64;
    result.sort_by_key(|line| {
        let top = line.bbox.rect.top().max(0);
        let left = line.bbox.rect.left().max(0);
        (top / 8, left)
    });

    let postprocess_started = Instant::now();
    let mut words = Vec::new();
    let mut lines = Vec::new();
    let mut word_order = 0u32;
    let mut used_jieba = false;

    for (line_index, line) in result.iter().enumerate() {
        let line_id = format!("fire-eye-line-{line_index}");
        let line_text = line.text.trim().to_string();
        if line_text.is_empty() {
            continue;
        }

        let line_polygon = polygon_from_result(line);
        let base_bbox = bbox_from_polygon(&line_polygon);
        let fallback_confidence = Some(line.confidence);
        let (line_words, line_used_jieba) = build_line_words(
            &line_id,
            &line_text,
            &base_bbox,
            fallback_confidence,
            &mut word_order,
        );
        used_jieba |= line_used_jieba;

        let line_confidence = average_confidence(line_words.iter().map(|word| word.confidence));
        let line_bbox = if line_words.is_empty() {
            base_bbox
        } else {
            union_bboxes(
                &line_words
                    .iter()
                    .map(|word| word.bbox.clone())
                    .collect::<Vec<_>>(),
            )
        };

        words.extend(line_words.iter().cloned());
        lines.push(NativeOcrLine {
            id: line_id,
            text: line_text,
            confidence: line_confidence,
            bbox: line_bbox.clone(),
            polygon: if line_polygon.is_empty() {
                polygon_from_bbox(&line_bbox)
            } else {
                line_polygon
            },
            word_ids: line_words.iter().map(|word| word.id.clone()).collect(),
            order: lines.len() as u32,
        });
    }

    let full_text = lines
        .iter()
        .map(|line| line.text.clone())
        .collect::<Vec<_>>()
        .join("\n")
        .trim()
        .to_string();
    let postprocess_ms = postprocess_started.elapsed().as_millis() as u64;

    Ok(NativeOcrResult {
        provider: "fire_eye_ocr".to_string(),
        text: full_text,
        confidence: average_confidence(lines.iter().map(|line| line.confidence)),
        width: result_width,
        height: result_height,
        words,
        lines,
        meta: json!({
            "engine": "ocr-rs",
            "backend": runtime.summary.backend,
            "models": runtime.summary.model_variant,
            "selectedBenchmarkMs": runtime.summary.benchmark_ms,
            "backendCandidates": scheduler.candidate_summaries(),
            "requestedLanguages": requested_languages,
            "wordSegmentation": if used_jieba { "jieba" } else { "native" },
            "embeddedModels": true,
            "sourceWidth": loaded.source_width,
            "sourceHeight": loaded.source_height,
            "inputKind": loaded.input_kind,
            "cropRect": loaded.crop_rect,
            "contrastEnhanced": loaded.contrast_enhanced,
            "timingsMs": {
                "load": load_ms,
                "recognize": recognize_ms,
                "postprocess": postprocess_ms,
                "total": load_ms + recognize_ms + postprocess_ms,
            },
        }),
    })
}

fn capabilities_from_scheduler(scheduler: &Scheduler) -> Result<FireEyeOcrCapabilities, String> {
    let runtime = scheduler
        .preferred_runtime()
        .ok_or_else(|| "火眼金睛当前没有可用的运行后端。".to_string())?;
    Ok(FireEyeOcrCapabilities {
        provider_id: "fire_eye_ocr".to_string(),
        available: true,
        error: None,
        language_tags: vec!["zh-Hans".to_string(), "en".to_string(), "ja".to_string()],
        language_names: vec![format!("火眼金睛 ({})", runtime.summary.backend)],
        max_image_dimension: None,
        recognizer_language_tag: Some(runtime.summary.model_variant.to_string()),
        recognizer_language_name: Some("火眼金睛".to_string()),
        selected_backend: Some(runtime.summary.backend.to_string()),
        selected_model_variant: Some(runtime.summary.model_variant.to_string()),
        benchmark_ms: Some(runtime.summary.benchmark_ms),
        backend_candidates: scheduler.candidate_summaries(),
    })
}

fn unavailable_capabilities(error: String) -> FireEyeOcrCapabilities {
    FireEyeOcrCapabilities {
        provider_id: "fire_eye_ocr".to_string(),
        available: false,
        error: Some(error),
        language_tags: Vec::new(),
        language_names: Vec::new(),
        max_image_dimension: None,
        recognizer_language_tag: None,
        recognizer_language_name: None,
        selected_backend: None,
        selected_model_variant: None,
        benchmark_ms: None,
        backend_candidates: Vec::new(),
    }
}

fn static_capabilities() -> FireEyeOcrCapabilities {
    match validate_assets() {
        Ok(()) => FireEyeOcrCapabilities {
            provider_id: "fire_eye_ocr".to_string(),
            available: true,
            error: None,
            language_tags: vec!["zh-Hans".to_string(), "en".to_string(), "ja".to_string()],
            language_names: vec!["火眼金睛".to_string()],
            max_image_dimension: None,
            recognizer_language_tag: Some("auto".to_string()),
            recognizer_language_name: Some("火眼金睛".to_string()),
            selected_backend: None,
            selected_model_variant: None,
            benchmark_ms: None,
            backend_candidates: Vec::new(),
        },
        Err(error) => unavailable_capabilities(error),
    }
}

fn capabilities() -> FireEyeOcrCapabilities {
    match build_scheduler().and_then(|scheduler| capabilities_from_scheduler(&scheduler)) {
        Ok(capabilities) => capabilities,
        Err(error) => unavailable_capabilities(error),
    }
}

fn run_request(request: WorkerRequest) -> WorkerResponse {
    run_request_with_state(request, None)
}

fn run_request_with_state(
    request: WorkerRequest,
    mut state: Option<&mut WorkerState>,
) -> WorkerResponse {
    match request.command.as_str() {
        "staticCapabilities" => WorkerResponse {
            ok: true,
            capabilities: Some(static_capabilities()),
            candidate: None,
            result: None,
            error: None,
        },
        "capabilities" => {
            let capabilities = if let Some(state) = state.as_deref_mut() {
                match state
                    .scheduler()
                    .and_then(|scheduler| capabilities_from_scheduler(scheduler))
                {
                    Ok(capabilities) => capabilities,
                    Err(error) => unavailable_capabilities(error),
                }
            } else {
                capabilities()
            };
            WorkerResponse {
                ok: true,
                capabilities: Some(capabilities),
                candidate: None,
                result: None,
                error: None,
            }
        }
        "warmup" => match if let Some(state) = state.as_deref_mut() {
            state.scheduler().map(|_| ())
        } else {
            build_scheduler().map(|_| ())
        } {
            Ok(_) => WorkerResponse {
                ok: true,
                capabilities: None,
                candidate: None,
                result: None,
                error: None,
            },
            Err(error) => response_error(error),
        },
        "recognize" => {
            let Some(input) = request.input else {
                return response_error("火眼金睛 worker 缺少 OCR 输入。");
            };
            let languages = request.languages.unwrap_or_default();
            let result = if let Some(state) = state.as_deref_mut() {
                state
                    .scheduler()
                    .and_then(|scheduler| recognize_with_scheduler(&input, &languages, scheduler))
            } else {
                recognize_with_candidate_fallback(&input, &languages)
            };
            match result {
                Ok(result) => WorkerResponse {
                    ok: true,
                    capabilities: None,
                    candidate: None,
                    result: Some(result),
                    error: None,
                },
                Err(error) => response_error(error),
            }
        }
        "probeCandidate" => {
            let (Some(backend), Some(model_variant)) =
                (request.backend.as_deref(), request.model_variant.as_deref())
            else {
                return response_error("火眼金睛候选探测缺少 backend 或 modelVariant。");
            };
            match resolve_candidate(backend, model_variant).and_then(probe_candidate) {
                Ok(candidate) => WorkerResponse {
                    ok: true,
                    capabilities: None,
                    candidate: Some(candidate),
                    result: None,
                    error: None,
                },
                Err(error) => response_error(error),
            }
        }
        "healthCandidate" => {
            let (Some(backend), Some(model_variant)) =
                (request.backend.as_deref(), request.model_variant.as_deref())
            else {
                return response_error("火眼金睛候选健康探测缺少 backend 或 modelVariant。");
            };
            match resolve_candidate(backend, model_variant).and_then(health_candidate) {
                Ok(candidate) => WorkerResponse {
                    ok: true,
                    capabilities: None,
                    candidate: Some(candidate),
                    result: None,
                    error: None,
                },
                Err(error) => response_error(error),
            }
        }
        "recognizeCandidate" => {
            let Some(input) = request.input else {
                return response_error("火眼金睛候选识别缺少 OCR 输入。");
            };
            let (Some(backend), Some(model_variant)) =
                (request.backend.as_deref(), request.model_variant.as_deref())
            else {
                return response_error("火眼金睛候选识别缺少 backend 或 modelVariant。");
            };
            let languages = request.languages.unwrap_or_default();
            match resolve_candidate(backend, model_variant)
                .and_then(create_runtime)
                .and_then(|runtime| {
                    let scheduler = Scheduler {
                        runtimes: vec![runtime],
                    };
                    recognize_with_scheduler(&input, &languages, &scheduler)
                }) {
                Ok(result) => WorkerResponse {
                    ok: true,
                    capabilities: None,
                    candidate: None,
                    result: Some(result),
                    error: None,
                },
                Err(error) => response_error(error),
            }
        }
        other => response_error(format!("未知的火眼金睛 worker 命令：{other}")),
    }
}

fn write_response(response: &WorkerResponse) {
    let json = serde_json::to_string(response).unwrap_or_else(|error| {
        format!(
            r#"{{"ok":false,"capabilities":null,"candidate":null,"result":null,"error":"序列化火眼金睛 worker 响应失败：{}"}}"#,
            error
        )
    });
    let _ = writeln!(std::io::stdout(), "{JSON_MARKER}{json}");
    let _ = std::io::stdout().flush();
}

fn run_daemon() {
    let stdin = std::io::stdin();
    let mut state = WorkerState::default();
    for line in stdin.lock().lines() {
        let response = match line {
            Ok(payload) if payload.trim().is_empty() => continue,
            Ok(payload) => match serde_json::from_str::<WorkerRequest>(&payload) {
                Ok(request) => run_request_with_state(request, Some(&mut state)),
                Err(error) => response_error(format!("解析火眼金睛 worker 输入失败：{error}")),
            },
            Err(error) => response_error(format!("读取火眼金睛 worker 输入失败：{error}")),
        };
        write_response(&response);
    }
}

fn main() {
    if std::env::args().any(|arg| arg == DAEMON_ARG) {
        run_daemon();
        return;
    }

    let mut payload = String::new();
    let response = match std::io::stdin().read_to_string(&mut payload) {
        Ok(_) => match serde_json::from_str::<WorkerRequest>(&payload) {
            Ok(request) => run_request(request),
            Err(error) => response_error(format!("解析火眼金睛 worker 输入失败：{error}")),
        },
        Err(error) => response_error(format!("读取火眼金睛 worker 输入失败：{error}")),
    };

    write_response(&response);
}
