import { describe, expect, it } from 'vitest';
import { validateCatalog, validateCredentials } from './validation';
import type { ConfigurationRequest } from './types';
function catalog(count = 1): ConfigurationRequest {
    return {
        authenticationEndpointId: 'server-0',
        allowInsecureLoopback: false,
        endpoints: Array.from({ length: count }, (_, index) => ({
            id: 'server-' + index,
            label: 'Server',
            address: 'https://localhost:2590',
        })),
    };
}
describe('input validation', () => {
    it('preserves meaningful whitespace and accepts exact credential limits', () => {
        expect(validateCredentials({ username: ' Admin ', password: ' fixture-only ' })).toEqual({});
        expect(validateCredentials({ username: 'a'.repeat(255), password: 'é'.repeat(512) })).toEqual({});
    });
    it.each([
        { username: 'a'.repeat(256), password: 'ok' },
        { username: 'Admin', password: 'é'.repeat(512) + 'x' },
        { username: ' ', password: 'ok' },
        { username: 'A\0B', password: 'ok' },
    ])('rejects invalid credentials', (input) => {
        expect(Object.keys(validateCredentials(input)).length).toBeGreaterThan(0);
    });
    it('accepts 16 unique endpoints but rejects 17', () => {
        expect(validateCatalog(catalog(16))).toEqual({});
        expect(Object.keys(validateCatalog(catalog(17))).length).toBeGreaterThan(0);
    });
    it.each([
        'https://user:pass@localhost',
        'https://host/path',
        'https://host/?query=1',
        'https://host/#fragment',
        'http://remote:2590',
    ])('rejects unsafe address %s', (address) => {
        const value = catalog();
        value.endpoints[0].address = address;
        expect(Object.keys(validateCatalog(value)).length).toBeGreaterThan(0);
    });
    it('rejects duplicate IDs, long labels and invalid authentication selection', () => {
        const value = catalog(2);
        value.endpoints[1].id = value.endpoints[0].id;
        value.endpoints[0].label = 'a'.repeat(101);
        value.authenticationEndpointId = 'missing';
        expect(Object.keys(validateCatalog(value)).length).toBeGreaterThan(0);
    });
    it('allows literal loopback HTTP only after explicit selection', () => {
        const value = catalog();
        value.endpoints[0].address = 'http://127.0.0.1:2590';
        expect(Object.keys(validateCatalog(value)).length).toBeGreaterThan(0);
        value.allowInsecureLoopback = true;
        expect(validateCatalog(value)).toEqual({});
    });
});
