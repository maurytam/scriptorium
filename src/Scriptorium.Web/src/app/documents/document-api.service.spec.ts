import { HttpEventType, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';

import { DocumentApiService } from './document-api.service';
import { DocumentDetails, DocumentSummary, UploadEvent } from './document.models';

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

  it('sends isPrivate=false by default and isPrivate=true when requested', () => {
    const file = new File(['hello'], 'notes.txt');

    service.upload(file).subscribe();
    const defaultRequest = http.expectOne('/api/documents');
    expect((defaultRequest.request.body as FormData).get('isPrivate')).toBe('false');
    defaultRequest.flush(details('processing'));

    service.upload(file, true).subscribe();
    const privateRequest = http.expectOne('/api/documents');
    expect((privateRequest.request.body as FormData).get('isPrivate')).toBe('true');
    privateRequest.flush(details('processing'));
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

  it('lists the documents from the server', () => {
    let received: DocumentSummary[] = [];

    service.list().subscribe((documents) => (received = documents));

    const request = http.expectOne('/api/documents');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'abc', fileName: 'a.pdf', fileType: 'pdf', uploadDate: '2026-08-03T12:00:00Z', isPrivate: true, status: 'ready' }]);
    expect(received.length).toBe(1);
    expect(received[0].isPrivate).toBeTrue();
  });

  it('notifies subscribers when documents change', () => {
    let notifications = 0;
    service.documentsChanged.subscribe(() => notifications++);

    service.notifyDocumentsChanged();
    service.notifyDocumentsChanged();

    expect(notifications).toBe(2);
  });

  it('deletes a document by id', () => {
    let completed = false;

    service.delete('abc').subscribe({ complete: () => (completed = true) });

    const request = http.expectOne('/api/documents/abc');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(completed).toBeTrue();
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
