// Run with node tests/canvas-bench.cjs from the editor workspace; requires Playwright + Edge.
const { chromium } = require('playwright');
(async () => {
  const { createServer } = await import('vite');
  const server = await createServer({server:{host:'127.0.0.1',port:0,open:false}});
  await server.listen();
  let browser;
  try {
    browser=await chromium.launch({channel:'msedge',headless:true});
    const page=await browser.newPage({viewport:{width:1280,height:800}});
    const errors=[]; page.on('pageerror',error=>errors.push(error.message));
    await page.goto(server.resolvedUrls.local[0]+'tests/canvas-500.html');
    await page.waitForFunction(()=>typeof window.measureCanvas==='function');
    const runs=[];
    for (let i=0;i<8;i++) {
      const sample=await page.evaluate(()=>window.measureCanvas());
      if(i) runs.push(sample);
      if(i<7) await page.evaluate(()=>window.disposeCanvas());
    }
    console.log('median mount + 3 animation frames, 1 warmup / 7 samples:', runs.sort((a,b)=>a.ms-b.ms)[3]);
    const first = page.locator('.node').first();
    const box = await first.boundingBox();
    const x=box.x+box.width/2, y=box.y+box.height/2;
    await page.mouse.move(x,y); await page.mouse.down();
    const frames=await first.evaluate(async (el, pos) => {
      const intervals=[], work=[]; let previous=0;
      for (let i=0;i<45;i++) {
        const t=await new Promise(requestAnimationFrame);
        const start=performance.now();
        el.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:1,clientX:pos.x+8+i,clientY:pos.y+4}));
        await Promise.resolve(); el.getBoundingClientRect();
        if (i>4) { intervals.push(t-previous);work.push(performance.now()-start); }
        previous=t;
      }
      if (!el.classList.contains('dragging')) throw Error('fixture did not enter drag');
      return {intervalP95:intervals.sort((a,b)=>a-b)[37],workP95:work.sort((a,b)=>a-b)[37]};
    },{x,y});
    await page.mouse.up();
    console.log('single-node drag, 40 measured frames:', frames);
    // Pan past the initial slice, then return. Culling must not truncate the document.
    const before=await first.getAttribute('aria-label');
    await page.mouse.move(20,700); await page.mouse.down({button:"middle"}); await page.mouse.move(20,30,{steps:6}); await page.mouse.up({button:"middle"});
    await page.waitForFunction(label=>document.querySelector('.node')?.getAttribute('aria-label')!==label,before);

    if(errors.length) throw Error(errors.join('\n'));
  } finally { await browser?.close(); await server.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
