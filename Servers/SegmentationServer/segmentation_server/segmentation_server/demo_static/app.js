const fileInput = document.querySelector("#file");
const clearButton = document.querySelector("#clear");
const statusLine = document.querySelector("#status");
const stage = document.querySelector("#stage");
const canvas = document.querySelector("#canvas");
const context = canvas.getContext("2d");

let imageId = null;
let image = null;
let points = [];
let segments = [];
let requestSerial = 0;
let busy = false;

fileInput.addEventListener("change", () => {
  const file = fileInput.files && fileInput.files[0];
  if (file) {
    uploadImage(file);
  }
});

clearButton.addEventListener("click", () => {
  points = [];
  segments = [];
  draw();
  setStatus(image ? "Points cleared." : "Choose an image.");
  clearButton.disabled = points.length === 0;
});

canvas.addEventListener("contextmenu", (event) => {
  event.preventDefault();
});

canvas.addEventListener("pointerdown", (event) => {
  if (busy || !image || imageId === null) {
    return;
  }
  const point = imagePoint(event);
  const existing = points.findIndex((item) => Math.hypot(item.x - point.x, item.y - point.y) <= 8);
  if (existing >= 0) {
    points.splice(existing, 1);
  } else {
    points.push({
      x: point.x,
      y: point.y,
      label: event.button === 2 ? 0 : 1,
    });
  }
  clearButton.disabled = points.length === 0;
  draw();
  segment();
});

async function uploadImage(file) {
  const previousId = imageId;
  const serial = ++requestSerial;
  busy = true;
  points = [];
  segments = [];
  clearButton.disabled = true;
  setStatus("Uploading…");
  let response;
  try {
    response = await apiFetch("/api/images", {
      method: "POST",
      headers: { "Content-Type": file.type || "application/octet-stream" },
      body: file,
    });
  } catch (error) {
    if (serial === requestSerial) {
      busy = false;
      setStatus("Upload failed.");
    }
    return;
  }
  if (serial !== requestSerial) {
    return;
  }
  if (!response.ok) {
    busy = false;
    setStatus(await errorText(response));
    stage.hidden = image === null;
    return;
  }
  if (previousId !== null) {
    apiFetch(`/api/images/${previousId}`, { method: "DELETE" }).catch(() => {});
  }
  imageId = response.headers.get("X-Image-Id");
  try {
    const blob = await response.blob();
    image = await createImageBitmap(blob);
  } catch (error) {
    busy = false;
    setStatus("Could not display the image.");
    return;
  }
  canvas.width = image.width;
  canvas.height = image.height;
  stage.hidden = false;
  draw();
  busy = false;
  setStatus("Left-click foreground. Right-click background.");
}

async function segment() {
  if (imageId === null) {
    return;
  }
  const serial = ++requestSerial;
  const sent = points.slice();
  if (sent.length === 0) {
    segments = [];
    draw();
    setStatus("Points cleared.");
    return;
  }
  setStatus("Segmenting…");
  let response;
  try {
    response = await apiFetch(`/api/images/${imageId}/segment`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ points: sent }),
    });
  } catch (error) {
    if (serial === requestSerial) {
      setStatus("Segmentation failed.");
    }
    return;
  }
  if (serial !== requestSerial) {
    return;
  }
  if (!response.ok) {
    setStatus(await errorText(response));
    return;
  }
  let payload;
  try {
    payload = await response.json();
    segments = [];
    for (const segment of payload.segments || []) {
      if (!segment.mask_png_base64) {
        continue;
      }
      segments.push({
        x: segment.x,
        y: segment.y,
        score: segment.score,
        overlay: await maskOverlay(segment.mask_png_base64),
      });
    }
  } catch (error) {
    if (serial === requestSerial) {
      setStatus("Could not read the mask.");
    }
    return;
  }
  if (serial !== requestSerial) {
    return;
  }
  draw();
  const score = segments.length ? segments[0].score : null;
  setStatus(score === null ? "No mask." : `Score ${score.toFixed(3)}`);
}

async function maskOverlay(maskBase64) {
  const bytes = Uint8Array.from(atob(maskBase64), (char) => char.charCodeAt(0));
  const bitmap = await createImageBitmap(new Blob([bytes], { type: "image/png" }));
  const overlay = document.createElement("canvas");
  overlay.width = bitmap.width;
  overlay.height = bitmap.height;
  const overlayContext = overlay.getContext("2d");
  overlayContext.drawImage(bitmap, 0, 0);
  const pixels = overlayContext.getImageData(0, 0, overlay.width, overlay.height);
  for (let index = 0; index < pixels.data.length; index += 4) {
    const on = pixels.data[index] > 127;
    pixels.data[index] = 64;
    pixels.data[index + 1] = 200;
    pixels.data[index + 2] = 96;
    pixels.data[index + 3] = on ? 140 : 0;
  }
  overlayContext.putImageData(pixels, 0, 0);
  return overlay;
}

function draw() {
  context.clearRect(0, 0, canvas.width, canvas.height);
  if (!image) {
    return;
  }
  context.drawImage(image, 0, 0);
  for (const segment of segments) {
    if (segment.overlay) {
      context.drawImage(segment.overlay, segment.x, segment.y);
    }
  }
  for (const point of points) {
    context.beginPath();
    context.fillStyle = point.label === 1 ? "#3dde7a" : "#ff5c5c";
    context.arc(point.x, point.y, 5, 0, Math.PI * 2);
    context.fill();
    context.lineWidth = 1.5;
    context.strokeStyle = "#111";
    context.stroke();
  }
}

function imagePoint(event) {
  const rect = canvas.getBoundingClientRect();
  const scaleX = canvas.width / rect.width;
  const scaleY = canvas.height / rect.height;
  return {
    x: clamp(Math.round((event.clientX - rect.left) * scaleX), 0, canvas.width - 1),
    y: clamp(Math.round((event.clientY - rect.top) * scaleY), 0, canvas.height - 1),
  };
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function setStatus(text) {
  statusLine.textContent = text;
}

async function errorText(response) {
  try {
    const payload = await response.json();
    if (payload.error) {
      return payload.error;
    }
  } catch (error) {
    return `Request failed (${response.status})`;
  }
  return `Request failed (${response.status})`;
}
// Every request that changes anything carries X-Demo-Client, which a page on another origin
// cannot add without a CORS preflight the server never allows. When the server wants a token
// (401), ask for it once, keep it for this tab only, and repeat the request.
const TOKEN_KEY = "segmentation-demo-token";

async function apiFetch(url, options = {}) {
  const attempt = () => {
    const headers = { ...(options.headers || {}), "X-Demo-Client": "1" };
    const token = sessionStorage.getItem(TOKEN_KEY);
    if (token) {
      headers["X-Demo-Token"] = token;
    }
    return fetch(url, { ...options, headers });
  };
  let response = await attempt();
  if (response.status === 401) {
    const token = window.prompt("This demo needs its access token:");
    if (token) {
      sessionStorage.setItem(TOKEN_KEY, token.trim());
      response = await attempt();
    }
  }
  return response;
}
