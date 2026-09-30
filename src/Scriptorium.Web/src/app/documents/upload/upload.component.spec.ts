import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { NEVER, Subject, of, throwError } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentDetails, UploadEvent } from '../document.models';
import { UploadComponent } from './upload.component';

const document = (status: DocumentDetails['status'], failureReason: string | null = null): DocumentDetails => ({
  id: 'abc',
  fileName: 'report.pdf',
  fileType: 'pdf',
  fileSizeBytes: 10,
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status,
  failureReason,
});

const completed = (): UploadEvent => ({ kind: 'completed', document: document('processing') });

describe('UploadComponent', () => {
  let fixture: ComponentFixture<UploadComponent>;
  let component: UploadComponent;
  let api: jasmine.SpyObj<DocumentApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['upload', 'waitUntilProcessed', 'getLimits']);
    api.getLimits.and.returnValue(of({ maxSizeBytes: 1000 }));
    await TestBed.configureTestingModule({
      imports: [UploadComponent],
      providers: [{ provide: DocumentApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(UploadComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const selectFile = () => component.selectedFile.set(new File(['x'], 'report.pdf'));
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('disables the upload button until a file is selected', () => {
    const button = (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();

    selectFile();
    fixture.detectChanges();

    expect(button.disabled).toBeFalse();
  });

  it('does nothing when submitted without a file', () => {
    component.submit();

    expect(api.upload).not.toHaveBeenCalled();
    expect(component.state()).toBe('idle');
  });

  it('uploads, polls, and reports a ready document', () => {
    api.upload.and.returnValue(of(completed()));
    api.waitUntilProcessed.and.returnValue(of(document('ready')));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(api.waitUntilProcessed).toHaveBeenCalledWith('abc');
    expect(component.state()).toBe('done');
    expect(text()).toContain('report.pdf is ready');
  });

  it('shows the failure reason when processing fails', () => {
    api.upload.and.returnValue(of(completed()));
    api.waitUntilProcessed.and.returnValue(of(document('failed', 'Corrupted or unreadable PDF')));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(text()).toContain('Corrupted or unreadable PDF');
  });

  it('shows the upload percentage while the file is being sent', () => {
    const events = new Subject<UploadEvent>();
    api.upload.and.returnValue(events);
    selectFile();

    component.submit();
    events.next({ kind: 'progress', percent: 42 });
    fixture.detectChanges();

    expect(component.state()).toBe('uploading');
    expect(text()).toContain('Uploading… 42%');
    expect((fixture.nativeElement as HTMLElement).querySelector('progress')?.getAttribute('max')).toBe('100');
  });

  it('shows an indeterminate processing indicator once the upload completes', () => {
    api.upload.and.returnValue(of(completed()));
    api.waitUntilProcessed.and.returnValue(NEVER);
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(component.state()).toBe('processing');
    expect(text()).toContain('Processing…');
  });

  it('shows the server error message when the upload is rejected', () => {
    api.upload.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Unsupported file type' } })),
    );
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(text()).toContain('Unsupported file type');
  });

  it('shows a size message when the server rejects with a bare 413', () => {
    api.upload.and.returnValue(throwError(() => new HttpErrorResponse({ status: 413 })));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(text()).toContain('maximum allowed size');
  });

  it('shows a generic message for unexpected errors', () => {
    api.upload.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(text()).toContain('The upload failed');
  });

  it('blocks a file larger than the server limit before uploading it', () => {
    component.selectedFile.set(new File([new Uint8Array(2 * 1024 * 1024)], 'big.pdf'));
    component.maxSizeBytes.set(1024 * 1024);
    fixture.detectChanges();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;
    expect(text()).toContain('File exceeds the maximum size of 1 MB.');
    expect(button.disabled).toBeTrue();

    component.submit();
    expect(api.upload).not.toHaveBeenCalled();
  });

  it('allows a file within the server limit', () => {
    component.selectedFile.set(new File(['x'], 'small.pdf'));
    fixture.detectChanges();

    expect(component.tooLargeMessage()).toBeNull();
    expect(text()).not.toContain('exceeds');
  });

  it('skips the pre-check when the limits cannot be loaded and lets the server decide', () => {
    api.getLimits.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const other = TestBed.createComponent(UploadComponent).componentInstance;
    other.selectedFile.set(new File([new Uint8Array(5000)], 'big.pdf'));

    expect(other.maxSizeBytes()).toBeNull();
    expect(other.tooLargeMessage()).toBeNull();
  });

  it('explains a network-level failure, such as the server closing the connection', () => {
    api.upload.and.returnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(text()).toContain('may exceed the maximum size');
  });

  it('uploads as not private by default', () => {
    api.upload.and.returnValue(of(completed()));
    api.waitUntilProcessed.and.returnValue(NEVER);
    selectFile();

    component.submit();

    expect(api.upload).toHaveBeenCalledWith(jasmine.any(File), false);
  });

  it('uploads as private when the checkbox is ticked', () => {
    api.upload.and.returnValue(of(completed()));
    api.waitUntilProcessed.and.returnValue(NEVER);
    selectFile();
    const checkbox = (fixture.nativeElement as HTMLElement).querySelector('input[type=checkbox]') as HTMLInputElement;

    checkbox.click();
    fixture.detectChanges();
    component.submit();

    expect(component.isPrivate()).toBeTrue();
    expect(api.upload).toHaveBeenCalledWith(jasmine.any(File), true);
  });

  it('shows the private checkbox unticked initially and locks it while uploading', () => {
    const checkbox = (fixture.nativeElement as HTMLElement).querySelector('input[type=checkbox]') as HTMLInputElement;
    expect(checkbox.checked).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Mark as private');

    api.upload.and.returnValue(new Subject<UploadEvent>());
    selectFile();
    component.submit();
    fixture.detectChanges();

    expect(checkbox.disabled).toBeTrue();
  });
});
