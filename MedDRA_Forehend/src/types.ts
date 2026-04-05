export interface MeddraVersion {
  version: string;
  collectionName: string;
}

export interface UploadPreviewRow {
  rowNumber: number;
  rawTerm: string;
}

export interface UploadPreviewResponse {
  fileName: string;
  totalRows: number;
  rows: UploadPreviewRow[];
}

export interface CandidateTerm {
  rank: number;
  lltCode: string;
  lltName: string;
  ptCode: string;
  ptName: string;
  hltCode: string;
  hltName: string;
  hgltCode: string;
  hgltName: string;
  socCode: string;
  socName: string;
  score: number;
}

export interface EncodingResult {
  rawTerm: string;
  version: string;
  top1Score: number;
  usedAi: boolean;
  remark: string;
  candidates: CandidateTerm[];
}

export interface EncodingRunResponse {
  version: string;
  totalCount: number;
  results: EncodingResult[];
}
