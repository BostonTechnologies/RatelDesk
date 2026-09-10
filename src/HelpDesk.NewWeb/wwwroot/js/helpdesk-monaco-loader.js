let loadPromise;

export function ensureBlazorMonaco() {
  loadPromise ??= loadMonaco();
  return loadPromise;
}

async function loadMonaco() {
  await loadScript('_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js');

  if (!window.require) {
    throw new Error('Monaco AMD loader loaded, but window.require is unavailable.');
  }

  const vsPath = new URL(
    '_content/BlazorMonaco/lib/monaco-editor/min/vs',
    document.baseURI).href.replace(/\/$/, '');

  window.require.config({ paths: { vs: vsPath } });

  await new Promise((resolve, reject) => {
    window.require(
      ['vs/editor/editor.main'],
      resolve,
      error => reject(error instanceof Error
        ? error
        : new Error('Failed to load Monaco editor.main.')));
  });

  await loadScript('_content/BlazorMonaco/jsInterop.js');

  if (!window.monaco) {
    throw new Error('BlazorMonaco assets loaded, but window.monaco is unavailable.');
  }
}

function loadScript(source) {
  const absoluteSource = new URL(source, document.baseURI).href;
  const existing = Array.from(document.scripts)
    .find(script => script.src === absoluteSource);

  if (existing?.dataset.helpdeskLoaded === 'true') {
    return Promise.resolve();
  }

  if (existing?.dataset.helpdeskLoading === 'true') {
    return new Promise((resolve, reject) => {
      existing.addEventListener('load', resolve, { once: true });
      existing.addEventListener('error', reject, { once: true });
    });
  }

  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = source;
    script.async = false;
    script.dataset.helpdeskLoading = 'true';
    script.addEventListener('load', () => {
      script.dataset.helpdeskLoaded = 'true';
      delete script.dataset.helpdeskLoading;
      resolve();
    }, { once: true });
    script.addEventListener('error', () => {
      reject(new Error(`Failed to load ${source}`));
    }, { once: true });
    document.body.appendChild(script);
  });
}
