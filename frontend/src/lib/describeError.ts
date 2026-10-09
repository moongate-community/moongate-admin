import { ApiError } from "@/api/errors";

function messageFor(error: ApiError): string {
  if (error.status === 404) {
    return "Not found.";
  }
  switch (error.code) {
    case "upstream_unauthenticated":
      return "Authentication failed. Check the username and password.";
    case "configuration_required":
      return "The Moongate connection is not configured on the server.";
    case "upstream_unavailable":
    case "network_error":
      return "Moongate is unreachable. Try again later.";
    case "upstream_deadlineexceeded":
      return "Moongate did not answer in time.";
    case "upstream_alreadyexists":
      return "That username already exists.";
    case "upstream_permissiondenied":
    case "permission_denied":
      return "You do not have permission for this action.";
    default:
      return `Request failed (${error.code}).`;
  }
}

export function describeError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return "Unexpected error.";
  }
  const message = messageFor(error);
  return error.correlationId ? `${message} (reference ${error.correlationId})` : message;
}
