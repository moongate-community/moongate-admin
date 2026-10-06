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
  assert.equal(document.components.securitySchemes.SetupToken.type, 'apiKey');
  assert.equal(document.components.securitySchemes.SetupToken.name, 'X-Moongate-Setup-Token');
  assert.equal(document.components.securitySchemes.SetupToken.in, 'header');
  assert.deepEqual(document.paths['/api/configuration/setup'].post.security, [{ SetupToken: [] }]);
  assert.deepEqual(document.paths['/api/configuration/status'].get.security || [], []);
  const probeRequirements = document.paths['/api/configuration/test-connection'].post.security;
  assert.ok(probeRequirements.some(requirement => Object.hasOwn(requirement, 'AdminBearer')));
  assert.ok(probeRequirements.some(requirement => Object.hasOwn(requirement, 'SetupToken')));
  assert.ok(probeRequirements.every(requirement => Object.keys(requirement).length === 1));
  for (const path of ['/api/auth/login', '/api/auth/logout']) {
    const requirements = document.paths[path].post.security;
    assert.ok(requirements.some(requirement => Object.hasOwn(requirement, 'AdminBearer')));
    assert.ok(requirements.some(requirement => Object.keys(requirement).length === 0));
  }
  console.log('Swagger bootstrap, OpenAPI binding, JWT Bearer, and configuration setup authorization verified.');
})().catch(error => { console.error(error); process.exitCode = 1; });
