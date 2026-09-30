import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';

import { DocumentApiService } from './document-api.service';
import { DocumentDetails } from './document.models';

const details = (status: DocumentDetails['status']): DocumentDetails => ({
  id: 'abc',
  fileName: 'report.pdf',
  fileType: 'pdf',
  fileSizeBytes: 10,
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status,
  failureReason: status === 'failed' ? 'bad pdf' : null,
});

describe('DocumentApiService', () => {
  let service: DocumentApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(DocumentApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uploads the file as multipart form data', () => {
    const file = new File(['hello'], 'notes.txt', { type: 'text/plain' });

    service.upload(file).subscribe();

    const request = http.expectOne('/api/documents');
    expect(request.request.method).toBe('POST');
    expect((request.request.body as FormData).get('file')).toBeTruthy();
    request.flush(details('processing'));
  });

  it('polls until the document leaves the processing status', fakeAsync(() => {
    const emitted: DocumentDetails[] = [];

    service.waitUntilProcessed('abc', 1000).subscribe((d) => emitted.push(d));

    tick(0);
    http.expectOne('/api/documents/abc').flush(details('processing'));
    expect(emitted).toEqual([]);

    tick(1000);
    http.expectOne('/api/documents/abc').flush(details('ready'));
    expect(emitted.map((d) => d.status)).toEqual(['ready']);

    tick(5000);
    http.expectNone('/api/documents/abc');
    discardPeriodicTasks();
  }));

  it('emits failed documents with their failure reason', fakeAsync(() => {
    const emitted: DocumentDetails[] = [];

    service.waitUntilProcessed('abc', 1000).subscribe((d) => emitted.push(d));

    tick(0);
    http.expectOne('/api/documents/abc').flush(details('failed'));
    expect(emitted[0].failureReason).toBe('bad pdf');
    discardPeriodicTasks();
  }));
});
