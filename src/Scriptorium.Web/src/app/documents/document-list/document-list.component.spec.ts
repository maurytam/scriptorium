import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import { DocumentApiService } from '../document-api.service';
import { DocumentSummary } from '../document.models';
import { DocumentListComponent } from './document-list.component';

const doc = (id: string, status: DocumentSummary['status'], overrides: Partial<DocumentSummary> = {}): DocumentSummary => ({
  id,
  fileName: `${id}.pdf`,
  fileType: 'pdf',
  uploadDate: '2026-08-03T12:00:00Z',
  isPrivate: false,
  status,
  ...overrides,
});

describe('DocumentListComponent', () => {
  let api: jasmine.SpyObj<DocumentApiService>;
  let changed: Subject<void>;

  beforeEach(() => {
    changed = new Subject<void>();
    api = jasmine.createSpyObj<DocumentApiService>('DocumentApiService', ['list', 'delete', 'notifyDocumentsChanged'], {
      documentsChanged: changed.asObservable(),
    });
    api.notifyDocumentsChanged.and.callFake(() => changed.next());
    TestBed.configureTestingModule({
      imports: [DocumentListComponent],
      providers: [{ provide: DocumentApiService, useValue: api }],
    });
  });

  const create = () => {
    const fixture = TestBed.createComponent(DocumentListComponent);
    fixture.detectChanges();
    return fixture;
  };
  const text = (fixture: ReturnType<typeof create>) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('shows a message when there are no documents', () => {
    api.list.and.returnValue(of([]));

    const fixture = create();

    expect(text(fixture)).toContain('No documents uploaded yet.');
    expect((fixture.nativeElement as HTMLElement).querySelector('table')).toBeNull();
  });

  it('shows name, type, upload date and status of every document', () => {
    api.list.and.returnValue(of([doc('report', 'ready'), doc('broken', 'failed', { fileType: 'docx' })]));

    const fixture = create();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('report.pdf');
    expect(rows[0].textContent).toContain('pdf');
    expect(rows[0].textContent).toContain('2026');
    expect(rows[0].textContent).toContain('ready');
    expect(rows[1].textContent).toContain('docx');
    expect(rows[1].textContent).toContain('failed');
  });

  it('marks private documents with a Private label and leaves the others alone', () => {
    api.list.and.returnValue(of([doc('secret', 'ready', { isPrivate: true }), doc('public', 'ready')]));

    const fixture = create();

    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows[0].textContent).toContain('Private');
    expect(rows[1].textContent).not.toContain('Private');
  });

  it('keeps reloading while a document is processing and stops once all are final', fakeAsync(() => {
    api.list.and.returnValues(of([doc('a', 'processing')]), of([doc('a', 'processing')]), of([doc('a', 'ready')]));

    const fixture = create();
    expect(api.list).toHaveBeenCalledTimes(1);
    expect(text(fixture)).toContain('processing');

    tick(2000);
    expect(api.list).toHaveBeenCalledTimes(2);

    tick(2000);
    fixture.detectChanges();
    expect(api.list).toHaveBeenCalledTimes(3);
    expect(text(fixture)).toContain('ready');

    tick(20000);
    expect(api.list).toHaveBeenCalledTimes(3);
  }));

  it('does not poll when every document is already in a final status', fakeAsync(() => {
    api.list.and.returnValue(of([doc('a', 'ready'), doc('b', 'failed')]));

    create();
    tick(10000);

    expect(api.list).toHaveBeenCalledTimes(1);
  }));

  it('reloads when the upload component reports a new document', () => {
    api.list.and.returnValues(of([]), of([doc('new', 'ready')]));
    const fixture = create();
    expect(api.list).toHaveBeenCalledTimes(1);

    changed.next();
    fixture.detectChanges();

    expect(api.list).toHaveBeenCalledTimes(2);
    expect(text(fixture)).toContain('new.pdf');
  });

  it('shows an error and recovers on the next refresh', () => {
    api.list.and.returnValues(throwError(() => new Error('down')), of([doc('back', 'ready')]));
    const fixture = create();
    expect(text(fixture)).toContain('could not be loaded');

    changed.next();
    fixture.detectChanges();

    expect(text(fixture)).toContain('back.pdf');
    expect(text(fixture)).not.toContain('could not be loaded');
  });

  describe('deleting a document', () => {
    const click = (fixture: ReturnType<typeof create>, selector: string, index = 0) =>
      ((fixture.nativeElement as HTMLElement).querySelectorAll(selector)[index] as HTMLButtonElement).click();

    it('shows a trash button on every row, disabled while the document is processing', () => {
      api.list.and.returnValue(of([doc('done', 'ready'), doc('busy', 'processing')]));
      const fixture = create();

      const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.icon-button');
      expect(buttons.length).toBe(2);
      expect(buttons[0].getAttribute('aria-label')).toBe('Delete done.pdf');
      expect(buttons[0].disabled).toBeFalse();
      expect(buttons[1].disabled).toBeTrue();
      expect(buttons[1].title).toContain('processing');
    });

    it('asks for confirmation in the row and lets the user back out', () => {
      api.list.and.returnValue(of([doc('a', 'ready'), doc('b', 'failed')]));
      const fixture = create();

      click(fixture, '.icon-button', 1);
      fixture.detectChanges();
      expect(text(fixture)).toContain('Delete?');
      expect((fixture.nativeElement as HTMLElement).querySelectorAll('.confirm').length).toBe(1);
      expect(api.delete).not.toHaveBeenCalled();

      click(fixture, '.confirm button.secondary');
      fixture.detectChanges();
      expect(text(fixture)).not.toContain('Delete?');
      expect(api.delete).not.toHaveBeenCalled();
    });

    it('deletes after confirmation and reloads the list', () => {
      api.list.and.returnValues(of([doc('a', 'ready'), doc('b', 'ready')]), of([doc('b', 'ready')]));
      api.delete.and.returnValue(of(undefined));
      const fixture = create();

      click(fixture, '.icon-button', 0);
      fixture.detectChanges();
      click(fixture, '.confirm button.danger');
      fixture.detectChanges();

      expect(api.delete).toHaveBeenCalledOnceWith('a');
      expect(api.list).toHaveBeenCalledTimes(2);
      const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
      expect(rows.length).toBe(1);
      expect(rows[0].textContent).toContain('b.pdf');
      expect(text(fixture)).not.toContain('Delete?');
    });

    it('keeps the row and shows the server message when the deletion is refused', () => {
      api.list.and.returnValue(of([doc('a', 'ready')]));
      api.delete.and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'The document is still being processed and cannot be deleted yet.' } })),
      );
      const fixture = create();

      click(fixture, '.icon-button');
      fixture.detectChanges();
      click(fixture, '.confirm button.danger');
      fixture.detectChanges();

      expect(text(fixture)).toContain('still being processed');
      expect((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr').length).toBe(1);
      expect(text(fixture)).not.toContain('Delete?');
    });

    it('shows a generic message when the deletion fails without details', () => {
      api.list.and.returnValue(of([doc('a', 'ready')]));
      api.delete.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      const fixture = create();

      click(fixture, '.icon-button');
      fixture.detectChanges();
      click(fixture, '.confirm button.danger');
      fixture.detectChanges();

      expect(text(fixture)).toContain('could not be deleted');
    });
  });
});
