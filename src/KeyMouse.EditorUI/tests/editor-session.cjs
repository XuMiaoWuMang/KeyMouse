// Real App + Vite + headless Edge; only the native shell/file chooser is replaced.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
(async () => {
  const { createServer } = await import('vite');
  const server = await createServer({server: {host: '127.0.0.1', port: 0, open: false}});
  await server.listen();
  let browser;
  try {
    browser = await chromium.launch({channel:'msedge', headless:true});
    const page = await browser.newPage({viewport:{width:1280,height:800}});
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    const schema = fs.readFileSync(path.join(__dirname, '../../KeyMouse.Core.Flow.Validation/flow.schema.json'),'utf8');
    await page.addInitScript(value => { window.fixtureSchema = value; }, schema);
    await page.goto(server.resolvedUrls.local[0] + 'tests/editor-session.html');
    assert.match(await page.title(), /KeyMouse/);
    const button = name => page.getByRole('button', { name, exact:true });
    for (const [index, label] of ['关键录制','完全录制','管理员关键录制','管理员完全录制'].entries()) {
      if (index === 0) await button(label).click();
      else {
        await button('更多录制方式').click();
        await page.getByRole('menuitem', {name:label, exact:true}).click();
      }
      await page.waitForFunction(n => fixture.recordings.length === n, index + 1);
      await page.evaluate(() => fixture.crash());
      await button('更多录制方式').waitFor();
    }
    assert.deepEqual(await page.evaluate(() => fixture.recordings), [
      ['record_flow','key'], ['record_flow','full'], ['record_flow_elevated','key'], ['record_flow_elevated','full']
    ]);
    const nodes = page.locator('.node');
    await button('新建流程').click();
    await button('添加').click();
    await page.getByRole('menuitem', {name:/等待时间/}).click();
    await nodes.first().waitFor();
    await button('撤销').click();
    await nodes.first().waitFor({state:'detached'});
    await button('重做').click();
    await nodes.first().waitFor();
    const img = page.getByAltText('这一步当时的样子');
    async function openFile(id) {
      await button('打开').click();
      await page.locator('.doc .path').filter({hasText:`${id}\\flow.json`}).waitFor();
      assert.ok(await button('撤销').isDisabled() && await button('重做').isDisabled());
      await nodes.first().click();
    }
    await openFile('A');
    await img.waitFor();
    const first = await img.getAttribute('src');
    await openFile('B');
    await page.waitForFunction(() => !!fixture.deferred);
    assert.ok(await page.evaluate(url => fixture.released.includes(url), first), 'old Blob must be revoked');
    await openFile('C');
    await img.waitFor();
    await img.evaluate(el => el.decode());
    const current = await img.getAttribute('src');
    assert.notEqual(first, current, 'identical relative names belong to different files');
    await page.evaluate(async () => { fixture.deferred(); await new Promise(requestAnimationFrame); });
    assert.equal(await img.getAttribute('src'), current, 'late reply for B must not overwrite C');
    await button('播放').click();
    await page.waitForFunction(() => fixture.runStarted);
    await page.evaluate(() => fixture.crash());
    await page.getByText(/没拿到退出码/).first().waitFor();
    assert.ok(await button('播放').isEnabled(), 'an interrupted run must not leave the toolbar locked');
    await openFile('Large');
    const visible = await nodes.count();
    assert.ok(visible > 0 && visible < 500, 'the full editor must not mount all 500 nodes');
    const observed = await page.evaluate(() => ({reads: fixture.reads, unexpected: fixture.unexpected}));
    assert.deepEqual(observed.reads, ['A\\flow.json', 'B\\flow.json', 'C\\flow.json']);
    assert.deepEqual(observed.unexpected, []);
    assert.equal(await page.locator('vite-error-overlay').count(), 0);
    assert.deepEqual(errors, []);
    console.log(`PASS: new/edit/undo/redo/open; stale thumbnail rejected and Blob released; process exit shown; 500 steps / ${visible} mounted cards`);
  } finally { await browser?.close(); await server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
