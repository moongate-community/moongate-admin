export const MAX_USERNAME_LENGTH = 255;
export const MAX_PASSWORD_BYTES = 1024;

export function validateUsername(value: string): string | null {
  if (value.trim().length === 0) {
    return "Username is required.";
  }
  if (value.length > MAX_USERNAME_LENGTH) {
    return "Username must be at most 255 characters.";
  }
  if (value.includes("\0")) {
    return "Username cannot contain NUL characters.";
  }
  return null;
}

export function validatePassword(value: string): string | null {
  if (value.trim().length === 0) {
    return "Password is required.";
  }
  if (new TextEncoder().encode(value).length > MAX_PASSWORD_BYTES) {
    return "Password must be at most 1024 bytes.";
  }
  if (value.includes("\0")) {
    return "Password cannot contain NUL characters.";
  }
  return null;
}
