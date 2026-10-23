import { HttpErrorResponse } from '@angular/common/http';

import { describeAskFailure } from './ask-failure-message';

const failure = (status: number, body?: unknown) => new HttpErrorResponse({ status, error: body });

describe('describeAskFailure', () => {
  it('names the server being unreachable when there is no answer at all', () => {
    expect(describeAskFailure(failure(0))).toContain('Cannot reach the Scriptorium server');
  });

  it('says the document is gone for a 404', () => {
    expect(describeAskFailure(failure(404, { error: 'Document not found.' }))).toBe(
      'This document no longer exists. Close this panel and choose another one.',
    );
  });

  it('shows what the server said for an invalid question', () => {
    expect(describeAskFailure(failure(400, { error: 'The question is empty.' }))).toBe('The question is empty.');
  });

  it('shows what the server said for a document that is not ready', () => {
    expect(describeAskFailure(failure(409, { error: 'The document is not ready. Current status: processing.' }))).toBe(
      'The document is not ready. Current status: processing.',
    );
  });

  it('shows what the server said when the model is not available', () => {
    const message = 'The answering model is not available. Check that Ollama is running and the model is installed.';

    expect(describeAskFailure(failure(503, { error: message }))).toBe(message);
  });

  it('shows what the server said when the model is too slow', () => {
    expect(describeAskFailure(failure(504, { error: 'The model did not answer in time. Please try again.' }))).toBe(
      'The model did not answer in time. Please try again.',
    );
  });

  it('is generic for a 500', () => {
    expect(describeAskFailure(failure(500, { error: 'An unexpected error occurred.' }))).toBe(
      'The question could not be answered. Please try again.',
    );
  });

  it('is generic when a gateway answers a 504 with a body that is not the API error', () => {
    expect(describeAskFailure(failure(504, 'Error occurred while trying to proxy'))).toBe(
      'The question could not be answered. Please try again.',
    );
    expect(describeAskFailure(failure(503))).toBe('The question could not be answered. Please try again.');
  });
});
