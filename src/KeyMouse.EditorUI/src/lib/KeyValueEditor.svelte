<script lang="ts">
  /**
   * 名字 → 表达式 的小表：`for` 的 init/step、`call` 的 vars 都用它。
   *
   * 以前这些字段在检查器里只说"去 JSON 里写"——条件循环没有这一步就没法用。
   * 这里保持对象的插入顺序；键改名时其余项不乱。
   */
  export let value: Record<string, string> | undefined = undefined;
  export let onchange: (value: Record<string, string> | undefined) => void;
  export let keyPlaceholder = '名字';
  export let valuePlaceholder = '表达式';
  export let addLabel = '加一项';

  function emit(next: Record<string, string>): void {
    onchange(Object.keys(next).length > 0 ? next : undefined);
  }

  function rename(oldKey: string, newKey: string): void {
    const next: Record<string, string> = {};
    for (const [key, item] of Object.entries(value ?? {})) {
      next[key === oldKey ? newKey : key] = item;
    }
    emit(next);
  }

  function setValue(key: string, item: string): void {
    emit({ ...(value ?? {}), [key]: item });
  }

  function remove(key: string): void {
    const next = { ...(value ?? {}) };
    delete next[key];
    emit(next);
  }

  function add(): void {
    let index = 1;
    while (Object.hasOwn(value ?? {}, `k${index}`)) index += 1;
    emit({ ...(value ?? {}), [`k${index}`]: '' });
  }
</script>

<div class="kv">
  {#if !value || Object.keys(value).length === 0}
    <p class="empty">还没有。条件循环可以先不填初值，或加一项。</p>
  {:else}
    {#each Object.entries(value) as [key, item]}
      <div class="kv-row">
        <input
          class="kv-key"
          value={key}
          placeholder={keyPlaceholder}
          aria-label={keyPlaceholder}
          on:input={(event) => rename(key, event.currentTarget.value)}
        />
        <span class="eq">=</span>
        <input
          class="kv-value"
          value={item}
          placeholder={valuePlaceholder}
          aria-label={valuePlaceholder}
          on:input={(event) => setValue(key, event.currentTarget.value)}
        />
        <button class="kv-x" on:click={() => remove(key)} aria-label="删掉这一项" title="删掉这一项">×</button>
      </div>
    {/each}
  {/if}
  <button class="kv-add" on:click={add}>{addLabel}</button>
</div>

<style>
  .kv {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .empty {
    margin: 0;
    font-size: 12px;
    color: var(--kp-ink3);
  }

  .kv-row {
    display: grid;
    grid-template-columns: minmax(54px, 0.8fr) auto minmax(80px, 1.4fr) 22px;
    align-items: center;
    gap: 6px;
  }

  input {
    min-width: 0;
    box-sizing: border-box;
    padding: 6px 8px;
    border: 1px solid var(--kp-rule);
    border-radius: 4px;
    background: var(--kp-card);
    color: var(--kp-ink);
    font: inherit;
    font-size: 12px;
  }

  input:focus {
    outline: none;
    border-color: var(--kp-signal);
  }

  .kv-value {
    font-family: var(--kp-font-mono);
  }

  .eq {
    color: var(--kp-ink3);
    font-family: var(--kp-font-mono);
    font-size: 12px;
  }

  .kv-x {
    padding: 0;
    border: 0;
    background: transparent;
    color: var(--kp-ink3);
    font-size: 15px;
    line-height: 1;
    cursor: pointer;
  }

  .kv-x:hover {
    color: var(--kp-signal);
  }

  .kv-add {
    align-self: flex-start;
    padding: 2px 0;
    border: 0;
    border-bottom: 1px solid transparent;
    background: transparent;
    color: var(--kp-ink2);
    font: inherit;
    font-size: 12px;
    cursor: pointer;
  }

  .kv-add:hover {
    color: var(--kp-signal);
    border-bottom-color: var(--kp-signal);
  }
</style>
