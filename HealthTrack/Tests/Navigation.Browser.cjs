// Browser test code only; this file is never served by the application.
const { chromium } = require(process.argv[3]);
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const base = process.argv[2];
const prefix = process.env.HEALTHTRACK_TEST_PREFIX;
const password = 'Ht9!' + crypto.randomUUID();
const artifacts = path.resolve(__dirname, '../bin/BrowserChecks');
fs.mkdirSync(artifacts, { recursive: true });
function assert(ok, message) { if (!ok) throw new Error(message); console.log('PASS: ' + message); }
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const context = await browser.newContext({ javaScriptEnabled: false });
    const page = await context.newPage();
    async function navigation(role) {
      for (const width of [320, 375, 768, 1199, 1200, 1280, 1440, 1920]) {
        await page.setViewportSize({ width, height: 900 });
        await page.reload();
        const menu = page.locator('.healthtrack-menu');
        const desktop = width >= 1200;
        const toggle = page.locator('summary.navbar-toggler');
        assert(await toggle.isVisible() === !desktop, `${role} ${width}px hamburger visibility matches breakpoint`);
        if (!desktop) {
          assert(await menu.getAttribute('open') === null && !await page.locator('.healthtrack-links').isVisible(), `${role} ${width}px mobile menu initially collapsed`);
          await toggle.focus();
          await page.keyboard.press('Enter');
          assert(await menu.getAttribute('open') !== null, `${role} ${width}px menu opens by keyboard without JavaScript`);
        }
        assert(await page.locator('.healthtrack-links').isVisible(), `${role} ${width}px navigation links visible`);
        const geometry = await page.locator('nav').evaluate(nav => {
          const brand = nav.querySelector('.navbar-brand').getBoundingClientRect();
          const toggle = nav.querySelector('summary').getBoundingClientRect();
          const links = [...nav.querySelectorAll('a.nav-link,button.nav-link,.healthtrack-greeting span')].map(el => ({rect: el.getBoundingClientRect().toJSON(), height: el.clientHeight, scroll: el.scrollHeight}));
          return { width: innerWidth, scroll: document.documentElement.scrollWidth, brand: brand.toJSON(), toggle: toggle.toJSON(), links };
        });
        assert(geometry.brand.left >= 0 && (desktop || geometry.brand.right <= geometry.toggle.left), `${role} ${width}px brand remains visible`);
        if (desktop) {
          const centers = geometry.links.map(x => x.rect.y + x.rect.height / 2);
          assert(Math.max(...centers) - Math.min(...centers) < 2, `${role} ${width}px all navigation/account items stay in one row`);
          assert(geometry.brand.right <= geometry.links[0].rect.left, `${role} ${width}px links follow brand without overlap`);
        }
        assert(geometry.scroll <= width && geometry.links.every(x => x.rect.left >= 0 && x.rect.right <= width && x.scroll <= x.height), `${role} ${width}px links fit without clipping or page overflow`);
        if (role !== 'Anonymous') {
          const cards = await page.locator('.dashboard-kpis .kpi-card').evaluateAll(elements => elements.map(el => el.getBoundingClientRect().toJSON()));
          const expected = { Client: 4, Trainer: 5, Admin: 7 }[role];
          assert(cards.length === expected, `${role} ${width}px displays all ${expected} KPI cards`);
          assert(cards.every(a => cards.filter(b => Math.abs(a.y - b.y) < 1).every(b => Math.abs(a.height - b.height) < 1)), `${role} ${width}px KPI card heights align within each row`);
          if (width < 576) assert(cards.every(card => Math.abs(card.x - cards[0].x) < 1), `${role} ${width}px KPI cards stack vertically`);
          assert(await page.locator('main h1').count() === 1, `${role} ${width}px has one main page heading`);
          assert(await page.locator('main .table').evaluateAll(tables => tables.every(table => table.parentElement.classList.contains('table-responsive'))), `${role} ${width}px tables use responsive containers`);
        }
        if (role !== 'Anonymous') assert(await page.getByRole('button', { name: 'Logout', exact: true }).isVisible(), `${role} ${width}px Logout visible`);
        if (width === 375 || width === 1280) await page.screenshot({ path: path.join(artifacts, `${role}-${desktop ? 'desktop' : 'mobile'}.png`), fullPage: true });
        if (!desktop) {
          await page.keyboard.press('Enter');
          assert(await menu.getAttribute('open') === null, `${role} ${width}px menu closes by keyboard`);
        }
      }
      const urls = await page.locator('nav a, main a').evaluateAll(links => [...new Set(links.map(a => a.href).filter(href => new URL(href).origin === location.origin && !new URL(href).hash))]);
      for (const url of urls) {
        const response = await context.request.get(url);
        assert(response.status() === 200, `${role} dashboard link ${new URL(url).pathname} resolves successfully`);
      }
    }
    async function logout() {
      if (await page.locator('summary.navbar-toggler').isVisible()) await page.locator('summary.navbar-toggler').click();
      await page.getByRole('button', { name: 'Logout', exact: true }).click();
      await page.waitForURL(base + '/');
    }
    async function fillAccount(email) {
      await page.locator('#FullName').fill('Long Name ' + 'A'.repeat(90));
      await page.locator('#Email').fill(email);
      await page.locator('#Password').fill(password);
      await page.locator('#ConfirmPassword').fill(password);
    }
    await page.goto(base + '/');
    await navigation('Anonymous');
    await page.goto(base + '/Account/Register');
    await fillAccount(prefix + '-client@example.test');
    await page.getByRole('button', { name: 'Register', exact: true }).click();
    await page.waitForURL('**/Dashboard/Client');
    await navigation('Client');
    await logout();
    const secrets = JSON.parse(fs.readFileSync(path.join(process.env.APPDATA, 'Microsoft/UserSecrets/HealthTrack-Development-Identity/secrets.json'), 'utf8').replace(/^\uFEFF/, ''));
    await page.goto(base + '/Account/Login');
    await page.locator('#Email').fill(secrets['Admin:Email']);
    await page.locator('#Password').fill(secrets['Admin:Password']);
    await page.getByRole('button', { name: /Log in|Login|Sign in/i }).click();
    await page.waitForURL('**/Dashboard/Admin');
    await navigation('Admin');
    await page.goto(base + '/AdminReports/Print');
    await page.emulateMedia({ media: 'print' });
    await page.pdf({ path: path.join(artifacts, 'Admin-report.pdf'), format: 'A4', printBackground: true });
    assert(await page.locator('nav,footer,button,script').count() === 0, 'Admin printable report excludes navigation and controls');
    assert(fs.statSync(path.join(artifacts, 'Admin-report.pdf')).size > 1000, 'Browser renders Admin report to PDF');
    await page.emulateMedia({ media: 'screen' });
    await page.goto(base + '/AdminTrainers/Create');
    await fillAccount(prefix + '-trainer@example.test');
    await page.getByRole('button', { name: 'Create Trainer', exact: true }).click();
    await page.waitForURL('**/Dashboard/Admin');
    await logout();
    await page.goto(base + '/Account/Login');
    await page.locator('#Email').fill(prefix + '-trainer@example.test');
    await page.locator('#Password').fill(password);
    await page.getByRole('button', { name: /Log in|Login|Sign in/i }).click();
    await page.waitForURL('**/Dashboard/Trainer');
    await navigation('Trainer');
    const response = await page.goto(base + '/TrainerWorkouts/Details/2147483647');
    assert(response.status() === 404 && (await page.locator('h1').innerText()).includes("couldn't find"), 'Missing record returns friendly 404');
    const badRequest = await context.request.post(base + '/TrainerWorkouts/SetStatus/2147483647');
    assert(badRequest.status() === 400 && (await badRequest.text()).includes("couldn't process"), 'Missing CSRF token returns friendly 400');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
