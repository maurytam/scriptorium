import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter, switchMap, tap } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentDetails, UploadCompleted } from '../document.models';

const BYTES_PER_MEGABYTE = 1024 * 1024;

type UploadState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';

@Component({
  selector: 'app-upload',
  templateUrl: './upload.component.html',
  styleUrl: './upload.component.css',
})
export class UploadComponent {
  private readonly api = inject(DocumentApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly acceptedTypes = '.pdf,.docx,.xlsx,.txt';
  readonly selectedFile = signal<File | null>(null);
  readonly state = signal<UploadState>('idle');
  readonly uploadPercent = signal(0);
  readonly result = signal<DocumentDetails | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly maxSizeBytes = signal<number | null>(null);
  readonly tooLargeMessage = computed(() => {
    const file = this.selectedFile();
    const max = this.maxSizeBytes();
    return file && max !== null && file.size > max
      ? `File exceeds the maximum size of ${Math.floor(max / BYTES_PER_MEGABYTE)} MB.`
      : null;
  });

  constructor() {
    this.api
      .getLimits()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (limits) => this.maxSizeBytes.set(limits.maxSizeBytes),
        // Without the limit there is no pre-check; the server still validates every upload.
        error: () => undefined,
      });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.item(0) ?? null);
    this.state.set('idle');
    this.result.set(null);
    this.errorMessage.set(null);
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file || this.tooLargeMessage()) {
      return;
    }

    this.uploadPercent.set(0);
    this.state.set('uploading');
    this.api
      .upload(file)
      .pipe(
        tap((event) => event.kind === 'progress' && this.uploadPercent.set(event.percent)),
        filter((event): event is UploadCompleted => event.kind === 'completed'),
        tap(() => this.state.set('processing')),
        switchMap((event) => this.api.waitUntilProcessed(event.document.id)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (document) => {
          this.result.set(document);
          this.state.set('done');
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(this.describe(error));
          this.state.set('error');
        },
      });
  }

  private describe(error: HttpErrorResponse): string {
    if (error.error?.error) {
      return error.error.error;
    }

    if (error.status === 413) {
      return 'The file exceeds the maximum allowed size.';
    }

    return error.status === 0
      ? 'The upload was interrupted. The file may exceed the maximum size, or the server is unreachable.'
      : 'The upload failed. Please try again.';
  }
}
