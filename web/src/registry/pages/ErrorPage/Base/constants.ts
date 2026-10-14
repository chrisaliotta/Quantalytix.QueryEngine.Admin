export const DEFAULT_STATUS_CODE = 404;
export const DEFAULT_TITLE = 'Page Not Found';
export const DEFAULT_DESCRIPTION = "The page you're looking for doesn't exist or has been moved.";
/**
 * Default title and description per HTTP status code.
 * Used by ErrorPage variant components (ErrorPage400, ErrorPage404, etc.).
 */
export const ERROR_PAGE_VARIANTS: Record<number, { title: string; description: string }> = {
  400: {
    title: 'Bad Request',
    description:
      'The request could not be understood or was invalid. Please check your input and try again.',
  },
  401: {
    title: 'Unauthorized',
    description: 'You need to sign in to access this resource. Please log in and try again.',
  },
  402: {
    title: 'Payment Required',
    description:
      'This resource requires payment or a valid subscription. Please upgrade or complete payment to continue.',
  },
  403: {
    title: 'Access Denied',
    description: "You don't have permission to access this resource.",
  },
  404: {
    title: 'Page Not Found',
    description: "The page you're looking for doesn't exist or has been moved.",
  },
  405: {
    title: 'Method Not Allowed',
    description:
      'The request method is not supported for this resource. Please try a different action.',
  },
  408: {
    title: 'Request Timeout',
    description:
      'The request took too long to complete. Please check your connection and try again.',
  },
  409: {
    title: 'Conflict',
    description:
      'The request could not be completed because of a conflict with the current state. Please refresh and try again.',
  },
  410: {
    title: 'Gone',
    description: 'This resource is no longer available and has been permanently removed.',
  },
  429: {
    title: 'Too Many Requests',
    description: "You've made too many requests. Please wait a moment and try again.",
  },
  500: {
    title: 'Internal Server Error',
    description:
      "Something went wrong on our end. We're working to fix it. Please try again later.",
  },
  502: {
    title: 'Bad Gateway',
    description:
      'The server received an invalid response from an upstream server. Please try again later.',
  },
  503: {
    title: 'Service Unavailable',
    description:
      "The service is temporarily unavailable. We're working to restore it. Please try again later.",
  },
  504: {
    title: 'Gateway Timeout',
    description: 'The server did not receive a timely response. Please try again in a few moments.',
  },
};
