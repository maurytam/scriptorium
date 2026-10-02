export type DocumentStatus = 'processing' | 'ready' | 'failed';

export interface UploadedDocument {
  id: string;
  fileName: string;
  fileType: string;
  fileSizeBytes: number;
  uploadDate: string;
  isPrivate: boolean;
  status: DocumentStatus;
}

export interface DocumentDetails extends UploadedDocument {
  failureReason: string | null;
}

export interface UploadProgress {
  kind: 'progress';
  percent: number;
}

export interface UploadCompleted {
  kind: 'completed';
  document: UploadedDocument;
}

export type UploadEvent = UploadProgress | UploadCompleted;

export interface UploadLimits {
  maxSizeBytes: number;
}

export interface DocumentSummary {
  id: string;
  fileName: string;
  fileType: string;
  uploadDate: string;
  isPrivate: boolean;
  status: DocumentStatus;
}

export interface AskResponse {
  answer: string;
  truncated: boolean;
}
