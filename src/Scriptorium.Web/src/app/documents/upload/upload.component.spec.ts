import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentDetails } from '../document.models';
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

describe('UploadComponent', () => {
  let fixture: ComponentFixture<UploadComponent>;
  let component: UploadComponent;
  let api: jasmine.SpyObj<DocumentApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['upload', 'waitUntilProcessed']);
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
    api.upload.and.returnValue(of(document('processing')));
    api.waitUntilProcessed.and.returnValue(of(document('ready')));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(api.waitUntilProcessed).toHaveBeenCalledWith('abc');
    expect(component.state()).toBe('done');
    expect(text()).toContain('report.pdf is ready');
  });

  it('shows the failure reason when processing fails', () => {
    api.upload.and.returnValue(of(document('processing')));
    api.waitUntilProcessed.and.returnValue(of(document('failed', 'Corrupted or unreadable PDF')));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(text()).toContain('Corrupted or unreadable PDF');
  });

  it('shows the server error message when the upload is rejected', () => {
    api.upload.and.returnValue(throwError(() => ({ error: { error: 'Unsupported file type' } })));
    selectFile();

    component.submit();
    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(text()).toContain('Unsupported file type');
  });
});
