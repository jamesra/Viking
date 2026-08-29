/*
 * Viking Data Export portal.
 *
 * A static page. It builds URLs against the existing per-volume Export services and downloads
 * the result, so it adds no server of its own and cannot fail to start.
 *
 * Two things about the export service shape the whole design:
 *
 *   1. There is no volume in its routes. Each volume is a separate IIS application bound to one
 *      volume by configuration, so the volume becomes a path segment: /{Volume}/Export/...
 *
 *   2. Inputs travel in the query string. The POST actions declare a file parameter but read
 *      their IDs from the query anyway, so a long structure list has to be split across several
 *      requests to stay under the IIS query-string limit. See MAX_QUERY_BYTES below.
 */

'use strict';

/* ── Configuration ──────────────────────────────────────── */

const IDENTITY_VOLUME_TREE = 'https://identity.codepharm.net:6001/Permissions/UserAccessibleVolumeTree';

/*
 * Volumes known to host an Export application, used to seed the list. The identity server only
 * reports volumes granted to the Anonymous group, which today is a subset, so the two sources are
 * merged and every candidate is probed before it is offered.
 */
const SEED_VOLUMES = [
  'RC1', 'RC2', 'RPC1', 'RPC2', 'RPC3',
  'NeitzTemporalMonkey', 'NeitzInferiorMonkey', 'NeitzCPED', 'NeitzNM',
  'RC1Marshak', 'KwanZebra1', 'John', 'RC1Test'
];

const DEFAULT_VOLUME = 'RC1';

/*
 * IIS caps the query string near 2048 bytes by default. Staying meaningfully below that leaves
 * headroom for the path and the other parameters.
 */
const MAX_QUERY_BYTES = 1800;

/*
 * The service's route table: {Report}/{format}. The service also still answers the older
 * {Report}/Get{FORMAT}/{format} form that named the format twice, so links published before
 * August 2026 keep working, but there is no reason to generate them.
 *
 * An unmatched URL returns the instructions page with HTTP 200 rather than a 404, so a typo
 * here surfaces as a successful download of an HTML file. These must be exact.
 */
const REPORTS = {
  morphology: {
    label: 'Morphology',
    acceptsIds: true,
    options: ['stick'],
    formats: [
      { id: 'tlp',  path: 'Morphology/tlp',  label: 'TLP',  note: 'Tulip' },
      { id: 'json', path: 'Morphology/json', label: 'JSON', note: 'generic' }
    ]
  },
  network: {
    label: 'Network',
    acceptsIds: true,
    options: ['hops'],
    formats: [
      { id: 'dot',  path: 'Network/dot',  label: 'DOT',     note: 'Graphviz' },
      { id: 'tlp',  path: 'Network/tlp',  label: 'TLP',     note: 'Tulip' },
      { id: 'gml',  path: 'Network/gml',  label: 'GraphML', note: 'XML' },
      { id: 'json', path: 'Network/json', label: 'JSON',    note: 'generic' }
    ]
  },
  motif: {
    label: 'Motif',
    acceptsIds: false,
    options: [],
    formats: [
      { id: 'dot',  path: 'Motif/dot',  label: 'DOT',  note: 'Graphviz' },
      { id: 'tlp',  path: 'Motif/tlp',  label: 'TLP',  note: 'Tulip' },
      { id: 'json', path: 'Motif/json', label: 'JSON', note: 'generic' }
    ]
  }
};

/* ── State ──────────────────────────────────────────────── */

const state = {
  volumes: [],
  report: 'morphology',
  format: 'tlp',
  ids: []
};

const $ = (id) => document.getElementById(id);

/* ── Service root ───────────────────────────────────────── */

/** Where the volume applications live when this page is not being served alongside them. */
const DEFAULT_SERVICE_ROOT = 'https://websvc.codepharm.net';

/**
 * Resolves the root that volume applications hang from.
 *
 * In production the portal is served at /Export/ and the volume applications sit at
 * /{Volume}/Export/, so the root is this page's URL with its own trailing /Export segment
 * removed. Both then share an origin, which is what lets downloads use fetch.
 *
 * When the page is opened from a file or from a local development server there are no volume
 * applications beneath it, so it targets the production host instead. A ?root= parameter
 * overrides both, which is useful for pointing a local copy at a staging host.
 */
function deriveServiceRoot() {
  const override = new URLSearchParams(location.search).get('root');
  if (override) {
    return override.replace(/\/+$/, '');
  }

  const host = location.hostname;
  const isLocal = location.protocol === 'file:'
    || host === ''
    || host === 'localhost'
    || host === '127.0.0.1'
    || host === '[::1]';

  if (isLocal) {
    return DEFAULT_SERVICE_ROOT;
  }

  let path = location.pathname.replace(/\/index\.html?$/i, '');
  path = path.replace(/\/Export\/?$/i, '');
  path = path.replace(/\/$/, '');
  return location.origin + path;
}

const SERVICE_ROOT = deriveServiceRoot();

/**
 * Whether the export services share this page's origin.
 *
 * This decides how a download is performed. Same-origin uses fetch, which allows the response to
 * be inspected, batches to be sequenced, and failures to be reported precisely. Cross-origin
 * cannot use fetch, because the export service sends no CORS headers, so the browser is navigated
 * to the URL instead and the service's Content-Disposition header produces the download.
 */
const SAME_ORIGIN = (() => {
  try {
    return new URL(SERVICE_ROOT, location.href).origin === location.origin;
  } catch {
    return false;
  }
})();

/* ── ID parsing ─────────────────────────────────────────── */

/**
 * Extracts structure IDs from free text.
 *
 * Deliberately permissive: commas, semicolons, tabs, spaces, and line breaks all separate, so a
 * pasted spreadsheet column, a CSV fragment, and a dragged text file all behave the same. Only
 * non-negative integers survive, and the result is deduplicated and sorted.
 */
function parseIds(text) {
  if (!text) return [];
  const seen = new Set();
  const matches = text.match(/\d+/g);
  if (matches) {
    for (const m of matches) {
      const n = Number(m);
      if (Number.isSafeInteger(n) && n >= 0) seen.add(n);
    }
  }
  return [...seen].sort((a, b) => a - b);
}

/* ── URL building ───────────────────────────────────────── */

/**
 * Builds the export URL for a volume, report, format, and ID batch.
 *
 * IDs are joined with semicolons because that is the separator the service splits on. The
 * encoding matters: a literal comma is parsed as part of a single malformed token, which the
 * service resolves to nothing, and an empty ID set means "export the whole volume". A caller who
 * used commas would silently receive the entire volume instead of the structures requested.
 */
function buildUrl(volume, reportKey, formatId, ids) {
  const report = REPORTS[reportKey];
  const fmt = report.formats.find((f) => f.id === formatId) || report.formats[0];
  const base = `${SERVICE_ROOT}/${volume}/Export/${fmt.path}`;

  const params = [];

  if (report.acceptsIds && ids && ids.length) {
    params.push('id=' + encodeURIComponent(ids.join(';')));
  }
  if (report.options.includes('hops')) {
    params.push('hops=' + encodeURIComponent($('hops').value || '1'));
  }
  if (report.options.includes('stick') && $('stick').checked) {
    params.push('stick=1');
  }

  return params.length ? `${base}?${params.join('&')}` : base;
}

/**
 * Splits IDs into batches whose encoded query stays within the server's limit.
 * Returns a single empty batch when there are no IDs, meaning "the whole volume".
 */
function batchIds(ids) {
  if (!ids.length) return [[]];

  const batches = [];
  let current = [];

  for (const id of ids) {
    const candidate = current.concat(id);
    // Semicolons encode to three characters each, so measure the encoded form rather than guess.
    const encodedLength = encodeURIComponent(candidate.join(';')).length;
    if (encodedLength > MAX_QUERY_BYTES && current.length) {
      batches.push(current);
      current = [id];
    } else {
      current = candidate;
    }
  }

  if (current.length) batches.push(current);
  return batches;
}

/* ── Volume discovery ───────────────────────────────────── */

/** Flattens the identity server's organizational-unit tree into a list of volumes. */
function flattenVolumeTree(nodes, groupName, out) {
  for (const node of nodes || []) {
    const group = node.name || groupName;
    for (const v of node.volumes || []) {
      out.push({
        name: v.name,
        group: group,
        description: (v.metadata && v.metadata.Description) || ''
      });
    }
    flattenVolumeTree(node.children, group, out);
  }
  return out;
}

/** Reads the anonymous volume tree. Returns an empty list if identity is unreachable. */
async function fetchIdentityVolumes() {
  try {
    const response = await fetch(IDENTITY_VOLUME_TREE, { mode: 'cors' });
    if (!response.ok) return [];
    return flattenVolumeTree(await response.json(), 'Volumes', []);
  } catch {
    // Most likely the CORS grant is not deployed yet. The seed list still gives a usable page.
    return [];
  }
}

/**
 * Tests whether a volume is online by asking its OData service, which is same-origin.
 *
 * The Export application itself cannot be probed usefully: it answers its help page with HTTP 200
 * even when it is misconfigured and every export fails, so a green result here means the volume
 * exists and is serving data, not that the export succeeded.
 */
async function probeVolume(name) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 12000);
  try {
    const response = await fetch(`${SERVICE_ROOT}/${name}/OData/`, { signal: controller.signal });
    return response.ok;
  } catch {
    return false;
  } finally {
    clearTimeout(timer);
  }
}

async function loadVolumes() {
  const select = $('volume');
  const status = $('volume-status');

  status.innerHTML = '<span class="dot wait"></span>Discovering volumes…';
  select.disabled = true;

  const identityVolumes = await fetchIdentityVolumes();

  // Merge identity's richer metadata over the seed names, keyed case-insensitively.
  const byKey = new Map();
  for (const name of SEED_VOLUMES) {
    byKey.set(name.toLowerCase(), { name, group: 'Volumes', description: '' });
  }
  for (const v of identityVolumes) {
    if (!v.name) continue;
    byKey.set(v.name.toLowerCase(), v);
  }

  const candidates = [...byKey.values()].sort((a, b) => a.name.localeCompare(b.name));

  const identityNote = identityVolumes.length
    ? ''
    : ' The identity service did not respond, so this list comes from the built-in set.';

  // Probing only works same-origin. Attempting it against a remote host produces thirteen
  // CORS failures that look identical to thirteen dead volumes, which would be misleading.
  if (!SAME_ORIGIN) {
    candidates.forEach((v) => { v.online = true; v.unverified = true; });
    state.volumes = candidates;
    renderVolumes();
    status.innerHTML = `<span class="dot wait"></span>Targeting <code>${SERVICE_ROOT}</code>. `
      + `Availability was not checked because that is a different origin from this page.${identityNote}`;
    select.disabled = false;
    return;
  }

  status.innerHTML = `<span class="dot wait"></span>Testing ${candidates.length} volumes…`;

  const online = await Promise.all(candidates.map((v) => probeVolume(v.name)));
  candidates.forEach((v, i) => { v.online = online[i]; });

  state.volumes = candidates;
  renderVolumes();

  const upCount = candidates.filter((v) => v.online).length;

  status.innerHTML = upCount
    ? `<span class="dot good"></span>${upCount} of ${candidates.length} volumes responding.${identityNote}`
    : `<span class="dot bad"></span>No volumes responded, so every entry below is listed as unverified. `
      + `All of them remain selectable.${identityNote}`;

  select.disabled = false;
}

function renderVolumes() {
  const select = $('volume');
  select.innerHTML = '';

  const groups = new Map();
  for (const v of state.volumes) {
    if (!groups.has(v.group)) groups.set(v.group, []);
    groups.get(v.group).push(v);
  }

  for (const [groupName, volumes] of groups) {
    const optgroup = document.createElement('optgroup');
    optgroup.label = groupName;
    for (const v of volumes) {
      const option = document.createElement('option');
      option.value = v.name;
      // The probe is advisory. A volume that did not answer stays selectable, because the probe
      // can fail for reasons that have nothing to do with the export service, and a page that
      // refuses to build a URL is less useful than one that lets the request fail honestly.
      option.textContent = (v.online || v.unverified) ? v.name : `${v.name} — did not respond`;
      optgroup.appendChild(option);
    }
    select.appendChild(optgroup);
  }

  // Without this the list simply opens on whatever sorts first, which is a private volume.
  const preferred = state.volumes.find(
    (v) => v.name.toLowerCase() === DEFAULT_VOLUME.toLowerCase() && (v.online || v.unverified));
  const firstOnline = state.volumes.find((v) => v.online);
  const fallback = firstOnline ? firstOnline.name : (state.volumes[0] ? state.volumes[0].name : '');
  select.value = preferred ? preferred.name : fallback;

  onVolumeChange();
}

function onVolumeChange() {
  const volume = $('volume').value;
  const found = state.volumes.find((v) => v.name === volume);

  let description = found && found.description ? found.description : '';
  if (found && !found.online && !found.unverified) {
    description = (description ? description + ' ' : '') +
      'This volume did not respond when the page loaded. You can still build and send the request.';
  }

  $('volume-desc').textContent = description;
  $('odata-link').href = `${SERVICE_ROOT}/${volume}/OData/`;
  refresh();
}

/* ── Rendering ──────────────────────────────────────────── */

function renderFormats() {
  const report = REPORTS[state.report];
  const container = $('formats');
  container.innerHTML = '';

  if (!report.formats.some((f) => f.id === state.format)) {
    state.format = report.formats[0].id;
  }

  for (const fmt of report.formats) {
    const label = document.createElement('label');
    label.className = 'fmt';
    label.innerHTML =
      `<input type="radio" name="format" value="${fmt.id}"${fmt.id === state.format ? ' checked' : ''}>` +
      `${fmt.label}<small>${fmt.note}</small>`;
    label.querySelector('input').addEventListener('change', () => {
      state.format = fmt.id;
      refresh();
    });
    container.appendChild(label);
  }

  $('format-hint').textContent =
    state.report === 'morphology' && state.format === 'json'
      ? 'Morphology JSON is currently returned empty by the service. Use TLP for full detail.'
      : '';
}

function renderOptions() {
  const report = REPORTS[state.report];

  $('motif-note').hidden = report.acceptsIds;
  $('options-body').hidden = !report.acceptsIds && !report.options.length;

  $('field-ids').hidden = !report.acceptsIds;
  $('field-hops').hidden = !report.options.includes('hops');
  $('field-stick').hidden = !report.options.includes('stick');
}

function renderIdCount() {
  const pill = $('id-count');
  const n = state.ids.length;
  if (!n) {
    pill.textContent = 'no IDs, whole volume';
    pill.classList.remove('count');
  } else {
    pill.textContent = `${n.toLocaleString()} structure${n === 1 ? '' : 's'}`;
    pill.classList.add('count');
  }
}

function renderRequest() {
  const volume = $('volume').value;
  if (!volume) {
    $('url-preview').value = '';
    $('download').disabled = true;
    return;
  }

  const report = REPORTS[state.report];
  const ids = report.acceptsIds ? state.ids : [];
  const batches = batchIds(ids);

  $('url-preview').value = buildUrl(volume, state.report, state.format, batches[0]);
  $('download').disabled = false;

  const bar = $('lengthbar');
  const fill = $('length-fill');
  const text = $('length-text');

  if (!ids.length) {
    bar.hidden = true;
    return;
  }

  bar.hidden = false;
  const used = encodeURIComponent(batches[0].join(';')).length;
  const pct = Math.min(100, (used / MAX_QUERY_BYTES) * 100);
  fill.style.width = pct + '%';
  fill.className = 'lengthbar-fill' + (pct > 90 ? ' warn' : '');

  if (batches.length > 1) {
    const caveat = state.report === 'network'
      ? ' Each part is exported independently, so hop traversal is computed within a part rather than across the whole list.'
      : '';
    text.textContent =
      `This list is too long for one request, so it will download as ${batches.length} separate ` +
      `files of up to ${batches[0].length} structures each.${caveat}`;
  } else {
    text.textContent = `Request uses ${used} of about ${MAX_QUERY_BYTES} available characters.`;
  }
}

function refresh() {
  renderFormats();
  renderOptions();
  renderIdCount();
  renderRequest();
}

/* ── Downloading ────────────────────────────────────────── */

function filenameFromResponse(response, fallback) {
  const header = response.headers.get('Content-Disposition') || '';
  const star = header.match(/filename\*=UTF-8''([^;]+)/i);
  if (star) {
    try { return decodeURIComponent(star[1]); } catch { /* fall through */ }
  }
  const plain = header.match(/filename="?([^";]+)"?/i);
  return plain ? plain[1] : fallback;
}

function saveBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 30000);
}

function addResult(name, size, ok, detail) {
  const results = $('results');
  results.hidden = false;
  if (!results.querySelector('h4')) {
    const heading = document.createElement('h4');
    heading.textContent = 'Downloads';
    results.appendChild(heading);
  }

  const row = document.createElement('div');
  row.className = 'result';
  row.innerHTML =
    `<span class="badge ${ok ? 'ok' : 'err'}">${ok ? 'saved' : 'failed'}</span>` +
    `<span class="name"></span>` +
    `<span class="size"></span>`;
  row.querySelector('.name').textContent = ok ? name : detail;
  row.querySelector('.size').textContent = ok ? formatBytes(size) : '';
  results.appendChild(row);
}

function formatBytes(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

async function downloadOne(url, fallbackName) {
  const response = await fetch(url);

  // An unmatched route returns the help page with HTTP 200, so a success status alone does not
  // mean an export was produced. Treat HTML on an export URL as a failure.
  const contentType = response.headers.get('Content-Type') || '';
  if (!response.ok) {
    let detail = `HTTP ${response.status}`;
    try {
      const body = await response.text();
      const problem = JSON.parse(body);
      if (problem.detail || problem.title) detail += ` — ${problem.detail || problem.title}`;
    } catch { /* body was not problem details */ }
    throw new Error(detail);
  }
  if (contentType.includes('text/html')) {
    throw new Error('The service returned its help page instead of a file, which means the URL did not match an export route.');
  }

  const blob = await response.blob();
  saveBlob(blob, filenameFromResponse(response, fallbackName));
  return { name: filenameFromResponse(response, fallbackName), size: blob.size };
}

/**
 * Starts a download by navigating to the URL.
 *
 * Used when the export service is on another origin, where fetch is blocked but a navigation is
 * not. The service marks its responses as attachments, so the browser saves the file and the page
 * stays put. Nothing can be reported about the outcome, because the response is never visible
 * to script.
 */
function saveByNavigation(url) {
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
}

async function onDownload() {
  const volume = $('volume').value;
  if (!volume) return;

  const button = $('download');
  const status = $('status');
  const report = REPORTS[state.report];
  const ids = report.acceptsIds ? state.ids : [];
  const batches = batchIds(ids);

  $('results').hidden = true;
  $('results').innerHTML = '';

  button.disabled = true;
  status.className = 'status';

  if (!SAME_ORIGIN) {
    // Browsers throttle rapid successive navigations, so space the batches out a little.
    for (let i = 0; i < batches.length; i++) {
      saveByNavigation(buildUrl(volume, state.report, state.format, batches[i]));
      if (i < batches.length - 1) {
        await new Promise((resolve) => setTimeout(resolve, 900));
      }
    }
    button.disabled = false;
    status.className = 'status';
    status.textContent = batches.length > 1
      ? `Started ${batches.length} downloads. Your browser may ask permission to save multiple files.`
      : 'Download started. Check your browser downloads for the result.';
    return;
  }

  let failures = 0;

  for (let i = 0; i < batches.length; i++) {
    const label = batches.length > 1 ? ` (part ${i + 1} of ${batches.length})` : '';
    status.innerHTML = `<span class="spinner"></span>Generating export${label}… this can take a while for large requests.`;

    const url = buildUrl(volume, state.report, state.format, batches[i]);
    try {
      const saved = await downloadOne(url, `${volume}-${state.report}.${state.format}`);
      addResult(saved.name, saved.size, true);
    } catch (error) {
      failures++;
      addResult('', 0, false, `Part ${i + 1}: ${error.message}`);
    }
  }

  button.disabled = false;

  if (!failures) {
    status.className = 'status ok';
    status.textContent = batches.length > 1 ? `All ${batches.length} parts downloaded.` : 'Download complete.';
  } else {
    status.className = 'status err';
    status.textContent = `${failures} of ${batches.length} request${batches.length === 1 ? '' : 's'} failed. `
      + 'If this volume is newly deployed its export service may not be configured yet.';
  }
}

/* ── Drag and drop ──────────────────────────────────────── */

function wireDropzone() {
  const zone = $('dropzone');
  const textarea = $('ids');
  const filesPill = $('id-files');
  const loaded = [];

  const stop = (e) => { e.preventDefault(); e.stopPropagation(); };

  ['dragenter', 'dragover'].forEach((type) =>
    zone.addEventListener(type, (e) => { stop(e); zone.classList.add('dragging'); }));

  ['dragleave', 'drop'].forEach((type) =>
    zone.addEventListener(type, (e) => { stop(e); zone.classList.remove('dragging'); }));

  zone.addEventListener('drop', async (e) => {
    const files = [...(e.dataTransfer.files || [])];
    if (!files.length) return;

    const texts = await Promise.all(files.map((f) => f.text()));
    const existing = textarea.value.trim();
    textarea.value = (existing ? existing + '\n' : '') + texts.join('\n');

    for (const f of files) loaded.push(f.name);
    filesPill.hidden = false;
    filesPill.textContent = `from ${loaded.join(', ')}`;

    onIdsChanged();
  });

  $('clear-ids').addEventListener('click', () => {
    textarea.value = '';
    loaded.length = 0;
    filesPill.hidden = true;
    onIdsChanged();
  });
}

function onIdsChanged() {
  state.ids = parseIds($('ids').value);
  renderIdCount();
  renderRequest();
}

/* ── Startup ────────────────────────────────────────────── */

function wire() {
  document.querySelectorAll('input[name=report]').forEach((radio) =>
    radio.addEventListener('change', () => {
      state.report = radio.value;
      refresh();
    }));

  $('ids').addEventListener('input', onIdsChanged);
  $('hops').addEventListener('input', renderRequest);
  $('stick').addEventListener('change', renderRequest);
  $('volume').addEventListener('change', onVolumeChange);
  $('download').addEventListener('click', onDownload);
  $('recheck').addEventListener('click', loadVolumes);

  $('copy-url').addEventListener('click', async () => {
    const value = $('url-preview').value;
    if (!value) return;
    try {
      await navigator.clipboard.writeText(value);
      $('copy-url').textContent = 'Copied';
      setTimeout(() => { $('copy-url').textContent = 'Copy'; }, 1500);
    } catch {
      $('url-preview').select();
    }
  });

  wireDropzone();
  refresh();
}

wire();
loadVolumes();
