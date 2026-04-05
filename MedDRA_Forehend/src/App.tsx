import { startTransition, useEffect, useState } from "react";
import {
  API_BASE_URL,
  downloadBlob,
  exportResults,
  fetchVersions,
  runBatchEncoding,
  runSingleEncoding,
  uploadExcel
} from "./api";
import type {
  EncodingResult,
  MeddraVersion,
  UploadPreviewResponse
} from "./types";

function formatScore(score: number | undefined): string {
  if (typeof score !== "number" || Number.isNaN(score)) {
    return "-";
  }

  return score.toFixed(4);
}

function collectTerms(preview: UploadPreviewResponse | null): string[] {
  return (preview?.rows ?? [])
    .map((row) => row.rawTerm.trim())
    .filter((term) => term.length > 0);
}

export default function App() {
  const pageSize = 10;
  const [versions, setVersions] = useState<MeddraVersion[]>([]);
  const [selectedVersion, setSelectedVersion] = useState("");
  const [preview, setPreview] = useState<UploadPreviewResponse | null>(null);
  const [results, setResults] = useState<EncodingResult[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [singleResult, setSingleResult] = useState<EncodingResult | null>(null);
  const [singleTerm, setSingleTerm] = useState("");
  const [selectedFileName, setSelectedFileName] = useState("");
  const [pageError, setPageError] = useState("");
  const [status, setStatus] = useState("正在加载 MedDRA 版本...");
  const [loadingVersions, setLoadingVersions] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [encodingBatch, setEncodingBatch] = useState(false);
  const [encodingSingle, setEncodingSingle] = useState(false);
  const [exporting, setExporting] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadVersions() {
      try {
        const data = await fetchVersions();
        if (!active) {
          return;
        }

        setVersions(data);
        setSelectedVersion(data[0]?.version ?? "");
        setStatus(data.length > 0 ? "版本加载完成，可以开始上传或即时编码。" : "未读取到可用版本。");
      } catch (error) {
        if (!active) {
          return;
        }

        setPageError(error instanceof Error ? error.message : "加载版本失败。");
        setStatus("无法连接后端，请检查 API 服务是否已启动。");
      } finally {
        if (active) {
          setLoadingVersions(false);
        }
      }
    }

    loadVersions();

    return () => {
      active = false;
    };
  }, []);

  const batchTerms = collectTerms(preview);
  async function handleUpload(file: File) {
    setUploading(true);
    setPageError("");
    setStatus(`正在解析 ${file.name} ...`);

    try {
      const data = await uploadExcel(file);
      startTransition(() => {
        setPreview(data);
        setResults([]);
        setCurrentPage(1);
        setSelectedFileName(file.name);
      });
      setStatus(`已解析 ${data.totalRows} 条术语，等待批量编码。`);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "上传失败。");
      setStatus("Excel 解析失败，请检查文件格式。");
    } finally {
      setUploading(false);
    }
  }

  async function handleRunBatch() {
    if (!selectedVersion) {
      setPageError("请先选择 MedDRA 版本。");
      return;
    }

    if (batchTerms.length === 0) {
      setPageError("请先上传并解析包含术语的 Excel。");
      return;
    }

    setEncodingBatch(true);
    setPageError("");
    setStatus(`正在批量编码 ${batchTerms.length} 条术语...`);

    try {
      const response = await runBatchEncoding({
        version: selectedVersion,
        terms: batchTerms
      });
      startTransition(() => {
        setResults(response.results);
        setCurrentPage(1);
      });
      setStatus(`批量编码完成，共返回 ${response.totalCount} 条结果。`);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "批量编码失败。");
      setStatus("批量编码失败，请检查后端日志。");
    } finally {
      setEncodingBatch(false);
    }
  }

  async function handleRunSingle() {
    if (!selectedVersion) {
      setPageError("请先选择 MedDRA 版本。");
      return;
    }

    if (singleTerm.trim().length === 0) {
      setPageError("请输入待编码术语。");
      return;
    }

    setEncodingSingle(true);
    setPageError("");
    setStatus(`正在即时编码术语：${singleTerm.trim()}`);

    try {
      const response = await runSingleEncoding({
        version: selectedVersion,
        term: singleTerm.trim()
      });
      startTransition(() => {
        setSingleResult(response);
      });
      setStatus("即时编码完成。");
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "即时编码失败。");
      setStatus("即时编码失败，请检查输入术语或后端状态。");
    } finally {
      setEncodingSingle(false);
    }
  }

  async function handleExport() {
    if (results.length === 0) {
      setPageError("当前没有可导出的批量编码结果。");
      return;
    }

    setExporting(true);
    setPageError("");
    setStatus("正在生成导出文件...");

    try {
      const blob = await exportResults(results);
      const timestamp = new Date().toISOString().slice(0, 19).replace(/[-:T]/g, "");
      downloadBlob(blob, `meddra-coding-result-${timestamp}.xlsx`);
      setStatus("导出完成。");
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "导出失败。");
      setStatus("导出失败，请稍后重试。");
    } finally {
      setExporting(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(results.length / pageSize));
  const pagedResults = results.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return (
    <div className="page-shell">
      <div className="hero-backdrop" />
      <main className="app-layout">
        <section className="hero-card">
          <div className="hero-copy">
            <p className="eyebrow">MedDRA Coding Workspace</p>
            <h1>MedDRA自动编码</h1>
            <p className="hero-text">
              支持MedDRA版本选择、单个术语编码、批量编码和结果导出
            </p>
          </div>
          <div className="hero-meta">
            <span className="meta-label">API</span>
            <code>{API_BASE_URL}</code>
            <span className={`status-pill${pageError ? " danger" : ""}`}>
              {loadingVersions ? "加载中" : pageError ? "连接异常或参数错误" : "已连接"}
            </span>
          </div>
        </section>

        <section className="panel version-panel">
          <div className="version-row">
            <div className="version-copy">
              <p className="panel-kicker">Version Control</p>
              <h2>MedDRA 版本选择</h2>
            </div>

            <label className="field version-field">
              <select
                value={selectedVersion}
                onChange={(event) => setSelectedVersion(event.target.value)}
                disabled={loadingVersions}
              >
                <option value="">请选择版本</option>
                {versions.map((item) => (
                  <option key={item.version} value={item.version}>
                    {item.version}
                  </option>
                ))}
              </select>
            </label>

            <span className="panel-badge version-badge">{selectedVersion || "未选择"}</span>
          </div>
        </section>

        <section className="control-grid">
          <article className="panel panel-tall">
            <div className="panel-header">
              <div>
                <p className="panel-kicker">Batch Workflow</p>
                <h2>批量编码</h2>
              </div>
              <span className="panel-badge">{preview?.totalRows ?? 0} 条</span>
            </div>

            <div className="batch-toolbar">
              <label className="upload-trigger">
                <input
                  type="file"
                  accept=".xlsx,.xls"
                  title="文件第一列的列名应为：Term、待编码术语、或原始术语"
                  onChange={(event) => {
                    const file = event.target.files?.[0];
                    if (file) {
                      void handleUpload(file);
                      event.target.value = "";
                    }
                  }}
                  disabled={uploading}
                />
                <span>{uploading ? "上传中..." : "选择文件"}</span>
              </label>

              <div className="toolbar-actions">
                <button
                  type="button"
                  className="primary-button"
                  onClick={() => void handleRunBatch()}
                  disabled={encodingBatch || batchTerms.length === 0}
                >
                  {encodingBatch ? "编码中..." : "批量运行"}
                </button>
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => void handleExport()}
                  disabled={exporting || results.length === 0}
                >
                  {exporting ? "导出中..." : "编码导出"}
                </button>
              </div>
            </div>

            <div className="preview-box">
              <div className="section-title">
                <h3>上传预览</h3>
              </div>
              <div className="table-shell compact">
                <table>
                  <thead>
                    <tr>
                      <th>原始术语</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview?.rows.length ? (
                      preview.rows.map((row) => (
                        <tr key={`${row.rowNumber}-${row.rawTerm}`}>
                          <td>{row.rawTerm}</td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={1} className="empty-cell">
                          上传 Excel 后在这里预览术语。
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </article>

          <article className="panel">
            <div className="panel-header">
              <div>
                <p className="panel-kicker">Instant Search</p>
                <h2>单词条即时编码</h2>
              </div>
              <span className="panel-badge">Top 3</span>
            </div>

            <div className="instant-input-row">
              <label className="field instant-input-field">
                <span>待编码术语</span>
                <textarea
                  rows={1}
                  value={singleTerm}
                  onChange={(event) => setSingleTerm(event.target.value)}
                  placeholder="例如：腹部压榨性疼痛"
                />
              </label>

              <button
                type="button"
                className="primary-button instant-run-button"
                onClick={() => void handleRunSingle()}
                disabled={encodingSingle}
              >
                {encodingSingle ? "编码中..." : "运行编码"}
              </button>
            </div>

            <div className="result-card single">
              <div className="section-title">
                <h3>即时结果</h3>
              </div>
              {singleResult && <SingleResultSummary result={singleResult} />}
            </div>
          </article>
        </section>

        <section className="panel result-panel">
          <p className="panel-kicker result-kicker">Batch Results</p>
          <div className="panel-header result-header-row">
            <div className="result-header-meta">
              <div className="result-header-pill">
                <div className="result-header-main">
                  <h2>批量编码结果</h2>
                </div>
                <span className="result-status-inline">{status}</span>
                {/*pageError ? <span className="result-error-inline">{pageError}</span> : null*/}
              </div>
              <span className="panel-badge">{results.length} 条结果</span>
            </div>
          </div>

          <div className="results-stack">
            {pagedResults.length > 0 ? (
              pagedResults.map((result) => <ResultSummary key={`${result.rawTerm}-${result.version}`} result={result} />)
            ) : (
              <div className="empty-panel">
                <p>批量编码结果会展示在这里。</p>
                <span>上传 Excel 并运行编码后，可以查看最优、次优、较优候选及是否触发 AI 重排。</span>
              </div>
            )}
          </div>

          {results.length > pageSize ? (
            <div className="pagination-bar">
              <button
                type="button"
                className="ghost-button pagination-button"
                onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                disabled={currentPage === 1}
              >
                上一页
              </button>
              <div className="pagination-info">
                <span>{currentPage} / {totalPages}</span>
              </div>
              <button
                type="button"
                className="ghost-button pagination-button"
                onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                disabled={currentPage === totalPages}
              >
                下一页
              </button>
            </div>
          ) : null}
        </section>
      </main>
    </div>
  );
}

function ResultSummary({ result }: { result: EncodingResult }) {
  return (
    <article className="result-card">
      <div className="result-topline">
        <div>
          <h3>{result.rawTerm}</h3>
        </div>
      </div>

      <div className="single-run-status batch-run-status">
        <span className={`ai-tag single-run-tag${result.usedAi ? " active" : ""}`}>{result.usedAi ? "已调用 AI 重排" : "未调用 AI"}</span>
        
        <div className="single-run-remark">
          <span>说明</span>
          <p>{result.remark || "无额外说明。"}</p>
        </div>
      </div>

      <div className="table-shell">
        {result.candidates.length > 0 ? (
          <div className="candidate-stack">
            {result.candidates.map((candidate) => (
              <article key={`${candidate.rank}-${candidate.lltCode}`} className="candidate-card">
                <div className="candidate-card-top">
                  <span className="candidate-rank">#{candidate.rank}</span>
                  <span className="candidate-score">分数 {formatScore(candidate.score)}</span>
                </div>

                <div className="candidate-path">
                  <div className="candidate-node">
                    <strong>LLT</strong>
                    <span>{candidate.lltName}</span>
                    <code>{candidate.lltCode}</code>
                  </div>
                  <div className="candidate-node">
                    <strong>PT</strong>
                    <span>{candidate.ptName}</span>
                    <code>{candidate.ptCode}</code>
                  </div>
                  <div className="candidate-node">
                    <strong>HLT</strong>
                    <span>{candidate.hltName}</span>
                    <code>{candidate.hltCode}</code>
                  </div>
                  <div className="candidate-node">
                    <strong>HLGT</strong>
                    <span>{candidate.hgltName}</span>
                    <code>{candidate.hgltCode}</code>
                  </div>
                  <div className="candidate-node">
                    <strong>SOC</strong>
                    <span>{candidate.socName}</span>
                    <code>{candidate.socCode}</code>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-cell">未返回候选结果。</div>
        )}
      </div>
    </article>
  );
}

function SingleResultSummary({ result }: { result: EncodingResult }) {
  return (
    <>
      <div className="single-run-status">
        <span className={`ai-tag single-run-tag${result.usedAi ? " active" : ""}`}>{result.usedAi ? "已调用 AI 重排" : "未调用 AI"}</span>
        <div className="single-run-remark">
          <span>说明</span>
          <p>{result.remark || "无额外说明。"}</p>
        </div>
      </div>

      <div className="single-results-strip">
        {result.candidates.length > 0 ? (
          result.candidates.map((candidate) => (
            <article key={`${candidate.rank}-${candidate.lltCode}`} className="candidate-card single-candidate-card">
              <div className="candidate-card-top single-candidate-top">
                <span className="candidate-rank">#{candidate.rank}</span>
                <span className="candidate-score">分数 {formatScore(candidate.score)}</span>
              </div>

              <div className="candidate-path single-candidate-path">
                <div className="candidate-node single-candidate-node">
                  <strong>LLT</strong>
                  <span>{candidate.lltName}</span>
                  <code>{candidate.lltCode}</code>
                </div>
                <div className="candidate-node single-candidate-node">
                  <strong>PT</strong>
                  <span>{candidate.ptName}</span>
                  <code>{candidate.ptCode}</code>
                </div>
                <div className="candidate-node single-candidate-node">
                  <strong>HLT</strong>
                  <span>{candidate.hltName}</span>
                  <code>{candidate.hltCode}</code>
                </div>
                <div className="candidate-node single-candidate-node">
                  <strong>HLGT</strong>
                  <span>{candidate.hgltName}</span>
                  <code>{candidate.hgltCode}</code>
                </div>
                <div className="candidate-node single-candidate-node">
                  <strong>SOC</strong>
                  <span>{candidate.socName}</span>
                  <code>{candidate.socCode}</code>
                </div>
              </div>
            </article>
          ))
        ) : (
          <div className="empty-cell">未返回候选结果。</div>
        )}
      </div>
    </>
  );
}
