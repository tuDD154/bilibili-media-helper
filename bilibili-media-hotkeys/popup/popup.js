"use strict";

const els = {
  statusDot: document.getElementById("status-dot"),
  statusText: document.getElementById("status-text"),
  tabCount: document.getElementById("tab-count"),
  videoTitle: document.getElementById("video-title"),
  timeNow: document.getElementById("time-now"),
  timeTotal: document.getElementById("time-total"),
  progressBar: document.getElementById("progress-bar"),
  hint: document.getElementById("hint"),
  iconPlay: document.getElementById("icon-play"),
  iconPause: document.getElementById("icon-pause"),
  toggleLabel: document.getElementById("toggle-label"),
  backSeconds: document.getElementById("back-seconds"),
  forwardSeconds: document.getElementById("forward-seconds"),
  keyToggle: document.getElementById("key-toggle"),
  keyBack: document.getElementById("key-back"),
  keyForward: document.getElementById("key-forward"),
  shortcutWarning: document.getElementById("shortcut-warning")
};

let hasVideo = false;
let busy = false;
let refreshing = false;

function sendMessage(message) {
  return new Promise((resolve) => {
    try {
      chrome.runtime.sendMessage(message, (response) => {
        const error = chrome.runtime.lastError;
        if (error) {
          resolve({ ok: false, reason: "no-background", error: error.message });
        } else {
          resolve(response);
        }
      });
    } catch (error) {
      resolve({ ok: false, reason: "no-background", error: String(error) });
    }
  });
}

function pad(value) {
  return String(value).padStart(2, "0");
}

function formatTime(seconds) {
  if (!Number.isFinite(seconds)) return "0:00";
  const total = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = total % 60;
  if (hours > 0) return hours + ":" + pad(minutes) + ":" + pad(secs);
  return minutes + ":" + pad(secs);
}

function setDot(kind) {
  els.statusDot.className = "dot" + (kind ? " " + kind : "");
}

function setHidden(element, hidden) {
  if (!element) return;
  if (hidden) {
    element.setAttribute("hidden", "");
  } else {
    element.removeAttribute("hidden");
  }
}

function setBusy(value) {
  busy = value;
  updateButtons();
}

function updateButtons() {
  document.querySelectorAll("[data-action]").forEach((button) => {
    button.disabled = busy || !hasVideo;
  });
}

function resetProgress() {
  els.timeNow.textContent = "0:00";
  els.timeTotal.textContent = "0:00";
  els.progressBar.style.width = "0%";
}

function renderError(result) {
  hasVideo = false;
  updateButtons();
  resetProgress();
  els.videoTitle.textContent = "打开B站视频页后，这里会显示标题";
  els.toggleLabel.textContent = "播放 / 暂停";
  setHidden(els.iconPlay, false);
  setHidden(els.iconPause, true);

  const reason = result && result.reason;
  const tabs = result && Number(result.tabCount);

  if (reason === "no-tab") {
    setDot("error");
    els.statusText.textContent = "没有检测到B站标签页";
    els.tabCount.textContent = "0 个B站标签";
    els.hint.textContent = "请先在浏览器里打开B站攻略视频，再回来使用快捷键。";
    return;
  }

  if (reason === "play-blocked") {
    setDot("error");
    els.statusText.textContent = "浏览器拦截了自动播放";
    els.tabCount.textContent = "";
    els.hint.textContent = "先切到B站页面手动点一次播放；之后快捷键就能正常控制。";
    return;
  }

  if (reason === "no-background") {
    setDot("error");
    els.statusText.textContent = "扩展后台未响应";
    els.tabCount.textContent = "";
    els.hint.textContent = "请重新加载扩展，或重启浏览器后再试。";
    return;
  }

  setDot("error");
  els.statusText.textContent = "未找到可控制的B站视频";
  els.tabCount.textContent = tabs > 0 ? tabs + " 个B站标签" : "";
  els.hint.textContent = "确认打开的是B站视频页，并手动点一次播放；直播页不支持快进/快退。";
}

function renderState(result) {
  const state = result.state;
  hasVideo = true;
  updateButtons();

  setDot(state.paused ? "paused" : "playing");
  els.statusText.textContent = state.paused ? "已暂停" : "正在播放";
  els.tabCount.textContent = "已连接";
  els.videoTitle.textContent = state.title || "B站视频";
  els.timeNow.textContent = formatTime(state.currentTime);
  els.timeTotal.textContent = state.duration ? formatTime(state.duration) : "直播";

  const percent =
    state.duration && state.duration > 0
      ? Math.min(100, Math.max(0, (state.currentTime / state.duration) * 100))
      : 0;
  els.progressBar.style.width = percent.toFixed(2) + "%";

  els.toggleLabel.textContent = state.paused ? "播放" : "暂停";
  setHidden(els.iconPlay, !state.paused);
  setHidden(els.iconPause, state.paused);
  els.hint.textContent = "快捷键在浏览器后台也有效，不会把焦点从游戏切走。";
}

async function refresh() {
  if (busy || refreshing) return;
  refreshing = true;
  const result =
    (await sendMessage({ type: "BILI_MEDIA_GET_STATUS" })) || {
      ok: false,
      reason: "no-background"
    };
  refreshing = false;

  if (result.ok && result.state) {
    renderState(result);
  } else {
    renderError(result);
  }
}

async function command(action) {
  if (busy) return;
  setBusy(true);

  const result = await sendMessage({ type: "BILI_MEDIA_POPUP_COMMAND", action });
  if (result && result.ok && result.state) {
    renderState(result);
  } else if (result && !result.ok) {
    renderError(result);
  }

  setBusy(false);
  await refresh();
}

function formatShortcut(shortcut) {
  if (!shortcut) return "—";
  return shortcut.split("+").join(" + ");
}

function loadShortcuts() {
  try {
    chrome.commands.getAll((commands) => {
      const map = {};
      for (const command of commands || []) {
        map[command.name] = command.shortcut;
      }

      els.keyToggle.textContent = formatShortcut(map["toggle-play"]);
      els.keyBack.textContent = formatShortcut(map["seek-backward"]);
      els.keyForward.textContent = formatShortcut(map["seek-forward"]);

      const missing = ["toggle-play", "seek-backward", "seek-forward"].some(
        (name) => !map[name]
      );
      els.shortcutWarning.hidden = !missing;
    });
  } catch (_) {
    els.shortcutWarning.hidden = false;
  }
}

async function loadSettings() {
  try {
    const stored = await chrome.storage.local.get({ seekStep: 3 });
    const step = Math.min(30, Math.max(1, Math.round(Number(stored.seekStep) || 3)));
    els.backSeconds.textContent = step + " 秒";
    els.forwardSeconds.textContent = step + " 秒";
  } catch (_) {
    // Keep the default labels.
  }
}

function shortcutsUrl() {
  const isEdge = /Edg\//.test(navigator.userAgent);
  return isEdge ? "edge://extensions/shortcuts" : "chrome://extensions/shortcuts";
}

function openShortcuts() {
  const url = shortcutsUrl();
  try {
    chrome.tabs.create({ url }, () => {
      const error = chrome.runtime.lastError;
      if (error) {
        copyShortcutsUrl(url);
      } else {
        window.close();
      }
    });
  } catch (_) {
    copyShortcutsUrl(url);
  }
}

async function copyShortcutsUrl(url) {
  try {
    await navigator.clipboard.writeText(url);
    els.hint.textContent = "快捷键设置地址已复制，请粘贴到地址栏打开。";
  } catch (_) {
    els.hint.textContent = "请手动打开：" + url;
  }
}

function openOptions() {
  try {
    chrome.runtime.openOptionsPage();
  } catch (_) {
    chrome.tabs.create({ url: chrome.runtime.getURL("options/options.html") });
  }
}

document.querySelectorAll("[data-action]").forEach((button) => {
  button.addEventListener("click", () => command(button.dataset.action));
});

document.getElementById("open-shortcuts").addEventListener("click", openShortcuts);
document.getElementById("open-options").addEventListener("click", openOptions);

try {
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local" || !changes.seekStep) return;
    const step = Math.min(30, Math.max(1, Math.round(Number(changes.seekStep.newValue) || 3)));
    els.backSeconds.textContent = step + " 秒";
    els.forwardSeconds.textContent = step + " 秒";
  });
} catch (_) {
  // Optional live refresh.
}

loadSettings();
loadShortcuts();
refresh();
setInterval(refresh, 1000);
