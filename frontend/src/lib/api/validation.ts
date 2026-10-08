import type { ConfigurationRequest, Credentials } from './types';
export function validateCredentials(value: Credentials): Record<string, string> {
    const errors: Record<string, string> = {};
    if (!value.username.trim() || value.username.includes('\0') || value.username.length > 255) {
        errors.username = 'Enter a username of at most 255 characters, without NUL characters.';
    }
    if (!value.password.trim() || value.password.includes('\0') || new TextEncoder().encode(value.password).length > 1024) {
        errors.password = 'Enter a password of at most 1024 UTF-8 bytes, without NUL characters.';
    }
    return errors;
}
export function validateCatalog(value: ConfigurationRequest): Record<string, string> {
    const errors: Record<string, string> = {};
    const ids = new Set<string>();
    if (!Array.isArray(value.endpoints) || value.endpoints.length < 1 || value.endpoints.length > 16) {
        errors.endpoints = 'Configure between 1 and 16 servers.';
        return errors;
    }
    value.endpoints.forEach((endpoint, index) => {
        const prefix = 'endpoints.' + index + '.';
        if (!endpoint || !/^[A-Za-z0-9._-]{1,64}$/.test(endpoint.id) || ids.has(endpoint.id)) {
            errors[prefix + 'id'] = 'Use a unique ID of 1–64 letters, digits, dots, underscores or hyphens.';
        }
        if (endpoint) {
            ids.add(endpoint.id);
        }
        if (!endpoint?.label?.trim() || endpoint.label.length > 100 || /[\u0000-\u001f\u007f-\u009f]/.test(endpoint.label)) {
            errors[prefix + 'label'] = 'Use a nonblank label of at most 100 characters without control characters.';
        }
        try {
            if (!endpoint?.address || endpoint.address.length > 2048) {
                throw new Error();
            }
            const address = new URL(endpoint.address);
            const hostname = address.hostname.replace(/^\[|\]$/g, '');
            const loopback = /^127(?:\.\d{1,3}){3}$/.test(hostname) || hostname === '::1';
            if (
                (address.protocol !== 'https:' &&
                    !(address.protocol === 'http:' && loopback && value.allowInsecureLoopback)) ||
                address.username ||
                address.password ||
                address.search ||
                address.hash ||
                address.pathname !== '/'
            ) {
                throw new Error();
            }
        } catch {
            errors[prefix + 'address'] = 'Use an HTTPS root address without credentials, query or fragment.';
        }
    });
    if (!ids.has(value.authenticationEndpointId)) {
        errors.authenticationEndpointId = 'Select a configured Login or Standalone server.';
    }
    return errors;
}
