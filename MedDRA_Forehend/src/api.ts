import type {
  EncodingResult,
  EncodingRunResponse,
  MeddraVersion,
  UploadPreviewResponse
} from "./types";

const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ||
  "http://localhost:5242";

async function parseError(response: Response): Promise<string> {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    try {
      const data = (await response.json()) as { message?: string; title?: string };
      return data.message || data.title || `请求失败，状态码 ${response.status}`;
    } catch {
      return `请求失败，状态码 ${response.status}`;
    }
  }

  const text = await response.text();
  return text || `请求失败，状态码 ${response.status}`;
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init);
  if (!response.ok) {
    throw new Error(await parseError(response));
  }

  return (await response.json()) as T;
}

export async function fetchVersions(): Promise<MeddraVersion[]> {
  return requestJson<MeddraVersion[]>("/api/meddra/versions");
}

export async function uploadExcel(file: File): Promise<UploadPreviewResponse> {
  const formData = new FormData();
  formData.append("file", file);

  return requestJson<UploadPreviewResponse>("/api/files/upload", {
    method: "POST",
    body: formData
  });
}

export async function runBatchEncoding(payload: {
  version: string;
  highConfidenceThreshold?: number;
  minimumScoreGap?: number;
  terms: string[];
}): Promise<EncodingRunResponse> {
  return requestJson<EncodingRunResponse>("/api/encoding/run", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(payload)
  });
}

export async function runSingleEncoding(payload: {
  version: string;
  term: string;
  highConfidenceThreshold?: number;
  minimumScoreGap?: number;
}): Promise<EncodingResult> {
  return requestJson<EncodingResult>("/api/encoding/single", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(payload)
  });
}

export async function exportResults(results: EncodingResult[]): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/api/files/export`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify(results)
  });

  if (!response.ok) {
    throw new Error(await parseError(response));
  }

  return response.blob();
}

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export { API_BASE_URL };
