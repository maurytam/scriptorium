import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { switchMap, tap } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentDetails } from '../document.models';

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
  readonly result = signal<DocumentDetails | null>(null);
  readonly errorMessage = signal<string | null>(null);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.item(0) ?? null);
    this.state.set('idle');
    this.result.set(null);
    this.errorMessage.set(null);
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.state.set('uploading');
    this.api
      .upload(file)
      .pipe(
        tap(() => this.state.set('processing')),
        switchMap((uploaded) => this.api.waitUntilProcessed(uploaded.id)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (document) => {
          this.result.set(document);
          this.state.set('done');
        },
        error: (error: HttpErrorResponse) => {
          this.errorMessage.set(error.error?.error ?? 'The upload failed. Please try again.');
          this.state.set('error');
        },
      });
  }
}
