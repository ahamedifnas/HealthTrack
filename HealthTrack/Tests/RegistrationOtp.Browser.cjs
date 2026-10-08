const { chromium } = require(process.argv[3]);
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const base = process.argv[2];
const prefix = process.env.HEALTHTRACK_OTP_TEST_PREFIX;
const output = path.resolve(__dirname, '../bin/RegistrationOtpBrowser');
fs.mkdirSync(output, { recursive: true });
function assert(ok, message) { if (!ok) throw new Error(message); console.log('PASS: ' + message); }
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const context = await browser.newContext();
    const page = await context.newPage();
    const password = 'Ht9!' + crypto.randomUUID();
    async function register(suffix) {
      await page.goto(base + '/Account/Register');
      await page.locator('#FullName').fill('Registration OTP browser test');
      await page.locator('#Email').fill(prefix + '-' + suffix + '@example.test');
      await page.locator('#Password').fill(password);
      await page.locator('#ConfirmPassword').fill(password);
      await page.getByRole('button', { name: 'Register', exact: true }).click();
      await page.waitForURL('**/Account/VerifyEmailOtp');
      const mails = await (await context.request.get('http://127.0.0.1:2527/')).json();
      const mail = mails.at(-1).replace(/=\r\n/g, '').replace(/=([0-9A-F]{2})/gi, (_, hex) => String.fromCharCode(parseInt(hex, 16)));
      return mail.match(/>([0-9]{6})<\/p>/)[1];
    }
    const code = await register('layout');
    for (const width of [320, 375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Verify Email fits ${width}px without horizontal overflow`);
      assert(await page.locator('#Code').getAttribute('autocomplete') === 'one-time-code', 'Verification field supports OTP autofill');
      assert(await page.locator('label[for="Code"]').isVisible(), 'OTP input has a visible associated label');
      if (width === 375 || width === 1440) await page.screenshot({ path: path.join(output, `verify-${width}.png`), fullPage: true });
    }
    assert(!(await page.content()).includes(code), 'Delivered OTP is not present in browser HTML');
    await page.locator('#Code').fill(code === '000000' ? '111111' : '000000');
    await page.getByRole('button', { name: 'Verify Email', exact: true }).click();
    assert(await page.locator('.auth-validation').innerText().then(text => text.includes('incorrect')), 'Wrong OTP has readable validation');
    assert(await page.locator('#Code').inputValue() === '', 'Submitted OTP is cleared after failed verification');
    assert(await page.locator('.validation-summary-errors').evaluate(el => document.activeElement === el), 'Validation summary receives keyboard focus');
    await page.locator('#Code').fill(code);
    await page.getByRole('button', { name: 'Verify Email', exact: true }).click();
    await page.waitForURL('**/Account/Login');
    await page.screenshot({ path: path.join(output, 'verification-complete.png'), fullPage: true });
    assert(await page.locator('.alert-success').count() === 1, 'Successful verification returns to Login with confirmation');

    const raceCode = await register('concurrency');
    const csrf = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const wrong = raceCode === '000000' ? '111111' : '000000';
    const responses = await Promise.all(Array.from({ length: 8 }, () => context.request.post(base + '/Account/VerifyEmailOtp', { form: { Code: wrong, __RequestVerificationToken: csrf } })));
    assert(responses.every(response => response.status() === 200), 'Concurrent wrong attempts return safe validation responses');
    const blocked = await context.request.post(base + '/Account/VerifyEmailOtp', { form: { Code: raceCode, __RequestVerificationToken: csrf } });
    assert((await blocked.text()).includes('Too many incorrect attempts'), 'Concurrent requests cannot bypass the five-attempt limit');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
