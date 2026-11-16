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

/** One earlier question and answer, sent back with the next question so the model has the context. */
export interface AskExchange {
  question: string;
  answer: string;
}

/** Which model the person wants to answer; Auto lets the server decide. Sent with every question. */
export type AskPreference = 'auto' | 'local' | 'cloud';

export interface AskResponse {
  answer: string;
  truncated: boolean;
  /** Which model produced the answer: the one on this computer or the cloud one. */
  backend: 'local' | 'cloud';
}

export interface AskLimits {
  maxQuestionLength: number;
}

/** The extracted text of a document, cut by the server at the limit the model reads. */
export interface TextPreview {
  documentId: string;
  content: string;
  truncated: boolean;
  totalCharacters: number;
}
