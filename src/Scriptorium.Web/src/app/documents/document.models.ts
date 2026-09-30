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
