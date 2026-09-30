import { HttpEventType, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';

import { DocumentApiService } from './document-api.service';
import { DocumentDetails, UploadEvent } from './document.models';

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
    expect(request.request.reportProgress).toBeTrue();
    expect((request.request.body as FormData).get('file')).toBeTruthy();
    request.flush(details('processing'));
  });

  it('emits upload progress percentages and then the completed document', () => {
    const events: UploadEvent[] = [];

    service.upload(new File(['hello'], 'notes.txt')).subscribe((e) => events.push(e));

    const request = http.expectOne('/api/documents');
    request.event({ type: HttpEventType.Sent });
    request.event({ type: HttpEventType.UploadProgress, loaded: 25, total: 100 });
    request.event({ type: HttpEventType.UploadProgress, loaded: 100, total: 100 });
    request.flush(details('processing'));

    expect(events.map((e) => e.kind)).toEqual(['progress', 'progress', 'completed']);
    expect(events[0]).toEqual({ kind: 'progress', percent: 25 });
    expect(events[1]).toEqual({ kind: 'progress', percent: 100 });
  });

  it('reports 0% progress when the total size is unknown', () => {
    const events: UploadEvent[] = [];

    service.upload(new File(['hello'], 'notes.txt')).subscribe((e) => events.push(e));

    http.expectOne('/api/documents').event({ type: HttpEventType.UploadProgress, loaded: 10 });

    expect(events).toEqual([{ kind: 'progress', percent: 0 }]);
  });

  it('propagates server rejections such as 400 and 413', () => {
    let status = 0;

    service.upload(new File(['x'], 'photo.png')).subscribe({ error: (e) => (status = e.status) });

    http.expectOne('/api/documents').flush({ error: 'nope' }, { status: 400, statusText: 'Bad Request' });

    expect(status).toBe(400);
  });

  it('reads the upload limits from the server', () => {
    let maxSizeBytes = 0;

    service.getLimits().subscribe((limits) => (maxSizeBytes = limits.maxSizeBytes));

    const request = http.expectOne('/api/documents/limits');
    expect(request.request.method).toBe('GET');
    request.flush({ maxSizeBytes: 31457280 });
    expect(maxSizeBytes).toBe(31457280);
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
