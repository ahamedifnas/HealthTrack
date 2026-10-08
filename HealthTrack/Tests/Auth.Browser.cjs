const { chromium } = require(process.argv[3]);
const fs = require('fs');
const path = require('path');
const base = process.argv[2] || 'http://localhost:5092';
const output = path.resolve(__dirname, '../bin/AuthBrowserChecks');
fs.mkdirSync(output, { recursive: true });
function assert(ok, message) { if (!ok) throw new Error(message); console.log('PASS: ' + message); }
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    for (const width of [320, 375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      for (const route of ['Login', 'Register', 'ForgotPassword', 'ForgotPasswordConfirmation', 'ResetPassword', 'ResetPasswordConfirmation', 'AccessDenied']) {
        await page.goto(base + '/Account/' + route);
        const geometry = await page.locator('.auth-card').evaluate(el => ({ width: innerWidth, scroll: document.documentElement.scrollWidth, rect: el.getBoundingClientRect().toJSON() }));
        assert(geometry.scroll <= width && geometry.rect.x >= 0 && geometry.rect.right <= width, `${route} fits ${width}px without overflow`);
        assert(await page.locator('h1').count() === 1, `${route} has a clear single page heading`);
        assert(await page.locator('input:not([type=hidden])').evaluateAll(inputs => inputs.every(input => document.querySelector('label[for="' + input.id + '"]'))), `${route} inputs have associated labels`);
        if (width === 375 || width === 1440) await page.screenshot({ path: path.join(output, `${route}-${width}.png`), fullPage: true });
      }
    }
    await page.goto(base + '/Account/Login');
    assert(await page.locator('#Email').evaluate(el => document.activeElement === el), 'Login email receives autofocus');
    await page.locator('#Password').fill('test-value');
    await page.getByRole('button', { name: 'Show password', exact: true }).focus();
    await page.keyboard.press('Enter');
    assert(await page.locator('#Password').getAttribute('type') === 'text', 'Keyboard activates password visibility');
    await page.keyboard.press('Enter');
    assert(await page.locator('#Password').getAttribute('type') === 'password', 'Keyboard hides password again');
    await page.getByRole('link', { name: 'Forgot password?' }).click();
    assert(page.url().endsWith('/Account/ForgotPassword'), 'Forgot password link works');
    await page.locator('#Email').fill('nobody@example.test');
    // Hold navigation so the disabled state can be inspected deterministically.
    await page.locator('form').evaluate(form => form.addEventListener('submit', event => event.preventDefault(), { once: true }));
    await page.getByRole('button', { name: 'Send Reset Link' }).click();
    assert(await page.getByRole('button', { name: 'Send Reset Link' }).isDisabled(), 'Submit button prevents repeated clicks during request');
    await page.reload();
    await page.locator('#Email').fill('nobody@example.test');
    await page.getByRole('button', { name: 'Send Reset Link' }).click();
    await page.waitForURL('**/ForgotPasswordConfirmation');
    const noJs = await browser.newContext({ javaScriptEnabled: false });
    const fallback = await noJs.newPage();
    await fallback.goto(base + '/Account/ForgotPassword');
    await fallback.locator('#Email').fill('nobody@example.test');
    await fallback.getByRole('button', { name: 'Send Reset Link' }).click();
    await fallback.waitForURL('**/ForgotPasswordConfirmation');
    assert(true, 'Recovery form works without JavaScript');
    await noJs.close();
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
