const vm = require('node:vm');
const assert = require('node:assert/strict');
let config;
const sandbox = {
  URL,
  window: { location: { href: 'https://localhost/swagger/index.html', origin: 'https://localhost' } },
  SwaggerUIBundle: Object.assign(options => { config = options; return { initOAuth() {} }; }, { presets: { apis: {} } }),
  SwaggerUIStandalonePreset: {}
};
(async () => {
  const base = process.argv[2] || 'http://127.0.0.1:17080';
  let response;
  for (let attempt = 0; attempt < 40; attempt++) {
    try {
      response = await fetch(new URL('/swagger/index.js', base));
      if (response.ok) break;
    } catch { /* The test host may still be starting. */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  assert.equal(response?.ok, true, 'Swagger initialization script must be served');
  vm.createContext(sandbox);
  vm.runInContext(await response.text(), sandbox);
  sandbox.window.onload();
  assert.equal(config.persistAuthorization, false);
  assert.equal(config.validatorUrl, null);
  assert.equal(new URL(config.urls[0].url).pathname, '/openapi/v1.json');
  const document = await (await fetch(new URL('/openapi/v1.json', base))).json();
  assert.equal(document.components.securitySchemes.AdminBearer.scheme, 'bearer');
  assert.equal(document.components.securitySchemes.AdminBearer.bearerFormat, 'JWT');
  assert.equal(document.paths['/api/auth/csrf'], undefined);
  console.log('Swagger bootstrap, OpenAPI binding, and JWT Bearer configuration verified.');
})().catch(error => { console.error(error); process.exitCode = 1; });
