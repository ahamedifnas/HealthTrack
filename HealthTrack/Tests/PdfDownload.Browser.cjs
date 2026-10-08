// Test-only tools live in ignored bin/PdfQA, never in the application bundle.
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const modules = path.resolve(__dirname, '../bin/PdfQA/node_modules');
const { chromium } = require(path.join(modules, 'playwright'));
const canvasApi = require(path.join(modules, '@napi-rs/canvas'));
Object.assign(globalThis, { DOMMatrix: canvasApi.DOMMatrix, ImageData: canvasApi.ImageData, Path2D: canvasApi.Path2D });
const base = process.argv[2];
const cases = JSON.parse(fs.readFileSync(process.argv[3], 'utf8').replace(/^\uFEFF/, ''));
const output = path.dirname(process.argv[3]);
const normalize = text => text.replace(/[\s\u200B\u00AD]+/g, '');
function assert(ok, text) { if (!ok) throw new Error(text); console.log('PASS: ' + text); }
(async () => {
    const pdfjs = await import(pathToFileURL(path.join(modules, 'pdfjs-dist/legacy/build/pdf.mjs')).href);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const evidence = [];
    const thumbnails = [];
    try {
        for (const email of [...new Set(cases.map(c => c.email))]) {
            const context = await browser.newContext({ acceptDownloads: true });
            const page = await context.newPage();
            await page.goto(base + '/Account/Login');
            await page.getByLabel('Email', { exact: true }).fill(email);
            await page.getByLabel('Password', { exact: true }).fill(process.env.HEALTHTRACK_PDF_TEST_PASSWORD);
            await Promise.all([page.waitForURL(url => !url.pathname.includes('/Account/Login')), page.getByRole('button', { name: 'Login', exact: true }).click()]);
            for (const item of cases.filter(c => c.email === email)) {
                await page.goto(base + item.path);
                const before = page.url();
                const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('link', { name: 'Download PDF', exact: true }).first().click()]);
                const filename = download.suggestedFilename();
                assert(/^HealthTrack_[a-zA-Z0-9_-]+_\d{4}-\d{2}-\d{2}\.pdf$/.test(filename), item.name + ' browser receives sanitized dated filename');
                const saved = path.join(output, item.name + '-browser.pdf');
                await download.saveAs(saved);
                assert(page.url() === before, item.name + ' click downloads without navigating to PDF preview');
                const loadingTask = pdfjs.getDocument({ data: new Uint8Array(fs.readFileSync(saved)), isEvalSupported: false });
                const pdf = await loadingTask.promise;
                let text = '';
                for (let number = 1; number <= pdf.numPages; number++) {
                    const pdfPage = await pdf.getPage(number);
                    const content = await pdfPage.getTextContent();
                    text += content.items.map(x => x.str).join(' ') + '\n';
                    const viewport = pdfPage.getViewport({ scale: 1.25 });
                    const canvas = canvasApi.createCanvas(Math.ceil(viewport.width), Math.ceil(viewport.height));
                    await pdfPage.render({ canvasContext: canvas.getContext('2d'), viewport }).promise;
                    const png = path.join(output, `${item.name}-page-${number}.png`);
                    fs.writeFileSync(png, canvas.toBuffer('image/png'));
                    thumbnails.push({ png, label: item.name + ' / ' + number });
                }
                assert(text.includes('HealthTrack') && text.includes('Generated:') && text.includes('Page'), item.name + ' saved PDF opens with report metadata and footer');
                if (item.include) assert(normalize(text).includes(normalize(item.include)), item.name + ' PDF contains permitted filtered fixture');
                if (item.exclude) assert(!normalize(text).includes(normalize(item.exclude)), item.name + ' PDF excludes filtered/unauthorized fixture');
                if (item.name === 'Client-Workout') assert(normalize(text).includes('END_OF_LONG_NOTE'), 'Long notes survive pagination without truncation');
                fs.writeFileSync(path.join(output, item.name + '.txt'), text);
                evidence.push({ report: item.name, filename, pages: pdf.numPages, download: true, dataVerified: true });
                await loadingTask.destroy();
            }
            await context.close();
        }
        const cellW = 290, cellH = 390, columns = 4;
        const sheet = canvasApi.createCanvas(cellW * columns, cellH * Math.ceil(thumbnails.length / columns));
        const ctx = sheet.getContext('2d'); ctx.fillStyle = '#dce4df'; ctx.fillRect(0, 0, sheet.width, sheet.height);
        for (let i = 0; i < thumbnails.length; i++) {
            const thumb = thumbnails[i], img = await canvasApi.loadImage(thumb.png);
            const x = (i % columns) * cellW, y = Math.floor(i / columns) * cellH;
            ctx.fillStyle = '#111'; ctx.font = '12px Arial'; ctx.fillText(thumb.label, x + 8, y + 17);
            const ratio = Math.min((cellW - 16) / img.width, (cellH - 30) / img.height);
            ctx.drawImage(img, x + 8, y + 25, img.width * ratio, img.height * ratio);
        }
        fs.writeFileSync(path.join(output, 'contact-sheet.png'), sheet.toBuffer('image/png'));
        fs.writeFileSync(path.resolve(__dirname, '../Documentation/Test-Results/Pdf-browser.json'), JSON.stringify(evidence, null, 2));
        console.log(`All ${evidence.length} PDF variants downloaded, opened, parsed and rendered in browser verification.`);
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
