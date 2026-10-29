<script lang="ts">
  /**
   * 步骤图标。
   *
   * 一套 16×16 的描边图标，颜色跟着父元素走（`currentColor`），所以选中行只要改文字色，
   * 图标就跟着变成朱砂。图形按"这一步在做什么"画，不按字段画：等时间就是钟，
   * 找字并点它就是放大镜盖住一行字。
   */
  export let name: string;
  export let size = 16;

  /** 每项是若干 `path d`，全部只描边不填充，笔画 1.25 才和 16px 的汉字对齐。 */
  const shapes: Record<string, string[]> = {
    // 切换窗口：一个窗口框，左边一支箭头指进去。
    focus: ['M6.5 3.5h6a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-6', 'M2 8h6.2M6 5.8 8.2 8 6 10.2'],
    // 输入文字：I 形光标 + 短横。
    type: ['M6 3.5h5M8.5 3.5v9M6.5 12.5h4', 'M4 5.5h-.8M4 10.5h-.8'],
    // 按键：键盘加一行键位。
    key: ['M2.5 5.5h11v5h-11z', 'M4.5 8h1M7 8h1M9.5 8h1M12 8h.01'],
    // 等待时间：钟。
    sleep: ['M8 2.8a5.2 5.2 0 1 0 0 10.4A5.2 5.2 0 0 0 8 2.8z', 'M8 5.6V8l1.8 1.2'],
    // 点击：指针。
    click: ['M5 2.8v7.6l1.9-1.8 1.4 3.1 1.5-.7-1.4-3.1h2.6z'],
    // 找字并点它：一行字 + 放大镜。
    'click-text': ['M2.5 5h7M2.5 8h4.5M2.5 11h3.5', 'M9.8 6.6a2.4 2.4 0 1 0 0 4.8 2.4 2.4 0 0 0 0-4.8z', 'M11.8 11.6 13.5 13.3'],
    // 等文字出现：一行字 + 闹钟指针。
    'wait-text': ['M2.5 4.5h6M2.5 7.5h3.5M2.5 10.5h2.5', 'M11 4.2a3.4 3.4 0 1 0 0 6.8 3.4 3.4 0 0 0 0-6.8z', 'M11 6v2l1.3.9'],
    // 等窗口出现：窗口框 + 时标。
    'wait-window': ['M2.5 4.5h7v7h-7z', 'M2.5 6.5h7', 'M11 4.2a3.4 3.4 0 1 0 0 6.8 3.4 3.4 0 0 0 0-6.8z', 'M11 6v2l1.3.9'],
    // 读屏取字：几行字 + 眼睛。
    'read-text': ['M2 4h8M2 6.5h5', 'M3.2 11.4c1.6-2 6.2-2 7.8 0', 'M7.1 10.6a1.1 1.1 0 1 0 0 2.1 1.1 1.1 0 0 0 0-2.1z'],
    // 滚轮：鼠标身 + 中键。
    wheel: ['M8 2.7c-1.9 0-3.2 1.5-3.2 4.2v2.5c0 2.1 1.3 3.4 3.2 3.4s3.2-1.3 3.2-3.4V6.9c0-2.7-1.3-4.2-3.2-4.2z', 'M8 4.9v2.3'],
    // 拖动：横向两个箭头。
    drag: ['M3 8h10M5.2 5.8 3 8l2.2 2.2M10.8 5.8 13 8l-2.2 2.2'],
    // 移动：四向。
    move: ['M8 2.6v10.8M2.6 8h10.8', 'M6.4 4.2 8 2.6l1.6 1.6M6.4 11.8 8 13.4l1.6-1.6M4.2 6.4 2.6 8l1.6 1.6M11.8 6.4 13.4 8l-1.6 1.6'],
    // 组合键：两个键并排。
    combo: ['M2.4 6h6v4.4h-6z', 'M7.6 8.6h6v4.4h-6z'],
    // 重复 N 次：环形箭头。
    repeat: ['M3.2 8a4.8 4.8 0 0 1 8.2-3.4', 'M12.8 8a4.8 4.8 0 0 1-8.2 3.4', 'M11.6 2.4v2.4H9.2', 'M4.4 13.6v-2.4h2.4'],
    // 对每一步做一遍：列表 + 环。
    foreach: ['M3 4.2h5M3 7.4h5M3 10.6h3.4', 'M11 5.4a3 3 0 1 0 0 6', 'M13.4 11.4 11 12.8l-.4-2.6'],
    // 调用另一条流程：框 + 出箭头。
    call: ['M2.6 4.6h6.2v6.8H2.6z', 'M9.4 8h4.2M11.8 6.2 13.6 8l-1.8 1.8'],
    // 按住 / 长按：向下按的箭头压在一条横线上。
    hold: ['M8 3v5.4M5.8 6.2 8 8.4l2.2-2.2', 'M3.4 11.6h9.2'],
    // 弹起 / 放开：向上箭头。
    release: ['M8 13V6.4M5.8 8.6 8 6.4l2.2 2.2', 'M3.4 3.4h9.2'],
    // 兜底：一个小方框，别让行里出现空洞。
    other: ['M4 4h8v8H4z'],
  };

  /** 步骤类型到图形名的映射；一个图形可以服务多个类型。 */
  const byType: Record<string, string> = {
    focus: 'focus',
    type: 'type',
    key: 'key',
    combo: 'combo',
    sleep: 'sleep',
    click: 'click',
    'click-text': 'click-text',
    'wait-text': 'wait-text',
    'wait-window': 'wait-window',
    'read-text': 'read-text',
    wheel: 'wheel',
    drag: 'drag',
    move: 'move',
    repeat: 'repeat',
    foreach: 'foreach',
    call: 'call',
    hold: 'hold',
    mousehold: 'hold',
    keyhold: 'hold',
    keydown: 'hold',
    keyup: 'release',
    mouseup: 'release',
    mousedown: 'hold',
  };

  $: paths = shapes[byType[name] ?? name] ?? shapes.other;
</script>

<svg
  class="step-icon"
  viewBox="0 0 16 16"
  width={size}
  height={size}
  fill="none"
  stroke="currentColor"
  stroke-width="1.25"
  stroke-linecap="round"
  stroke-linejoin="round"
  aria-hidden="true"
>
  {#each paths as d}
    <path {d} />
  {/each}
</svg>

<style>
  .step-icon {
    display: block;
    flex: none;
  }
</style>
