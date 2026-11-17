import { HttpErrorResponse } from '@angular/common/http';

const GENERIC = 'The question could not be answered. Please try again.';
const DOCUMENT_GONE = 'This document no longer exists. Close this panel and choose another one.';
const NOT_RECOGNISED =
  'The server did not recognise this request. Check that the API is up to date and running, then try again.';
const SERVER_UNREACHABLE = 'Cannot reach the Scriptorium server. Check that the API is running and try again.';

// The statuses whose server text tells the user something they can act on (see contracts/ask-api.md).
const SERVER_MESSAGE_STATUSES = [400, 409, 503, 504];

/** The text shown to the user when a question could not be answered. */
export function describeAskFailure(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return SERVER_UNREACHABLE;
  }

  if (error.status === 404) {
    // Only the API's own answer means the document is gone; a 404 with no such body is a route that does not exist
    // (for example an API started before the code was updated).
    return serverMessage(error) === null ? NOT_RECOGNISED : DOCUMENT_GONE;
  }

  return SERVER_MESSAGE_STATUSES.includes(error.status) ? (serverMessage(error) ?? GENERIC) : GENERIC;
}

// A proxy or gateway in front of the API may answer with a body that is not the API's `{ "error": "..." }`.
function serverMessage(error: HttpErrorResponse): string | null {
  const message = (error.error as { error?: unknown } | null)?.error;
  return typeof message === 'string' && message.length > 0 ? message : null;
}
