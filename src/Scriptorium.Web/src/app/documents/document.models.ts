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

/** A short summary of a document; produced each time, never stored. */
export interface SummaryResponse {
  summary: string;
  truncated: boolean;
  /** Which model wrote the summary: the one on this computer or the cloud one. */
  backend: 'local' | 'cloud';
}

/** The five kinds of entity, as the server names them; the server also decides their order. */
export type EntityType = 'people' | 'organizations' | 'dates' | 'amounts' | 'places';

export interface EntityGroup {
  type: EntityType;
  entities: string[];
}

/** The key entities of a document, grouped by type; produced each time, never stored. */
export interface EntitiesResponse {
  /** Only the types that have at least one entity; empty when nothing was found. */
  groups: EntityGroup[];
  truncated: boolean;
  /** Which model did the work: the one on this computer or the cloud one. */
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
