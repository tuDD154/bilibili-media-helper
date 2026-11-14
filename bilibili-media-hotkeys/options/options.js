"use strict";

const DEFAULT_SETTINGS = {
  seekStep: 3,
  showToast: true
};

const stepInput = document.getElementById("seek-step");
const toastInput = document.getElementById("show-toast");
const form = document.getElementById("settings-form");
const saveStatus = document.getElementById("save-status");
const shortcutList = document.getElementById("shortcut-list");
const versionLabel = document.getElementById("version");

let statusTimer = null;

function normalizeStep(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return DEFAULT_SETTINGS.seekStep;
  return Math.min(30, Math.max(1, Math.round(number)));
}

function showStatus(text, isError) {
  if (statusTimer) clearTimeout(statusTimer);
  saveStatus.textContent = text;
  saveStatus.className = "save-status" + (isError ? " error" : "");
  statusTimer = setTimeout(() => {
    saveStatus.textContent = "";
    statusTimer = null;
  }, 2600);
}

function updatePresets() {
  const current = normalizeStep(stepInput.value);
  document.querySelectorAll("[data-step]").forEach((button) => {
    button.classList.toggle("active", Number(button.dataset.step) === current);
  });
}

async function loadSettings() {
  try {
    const stored = await chrome.storage.local.get(DEFAULT_SETTINGS);
    stepInput.value = String(normalizeStep(stored.seekStep));
    toastInput.checked = stored.showToast !== false;
    updatePresets();
  } catch (_) {
    showStatus("读取设置失败", true);
  }
}

async function saveSettings(event) {
  event.preventDefault();
  const seekStep = normalizeStep(stepInput.value);
  const showToast = toastInput.checked;
  stepInput.value = String(seekStep);

  try {
    await chrome.storage.local.set({ seekStep, showToast });
    updatePresets();
    showStatus("已保存");
  } catch (_) {
    showStatus("保存失败，请重试", true);
  }
}

function formatShortcut(shortcut) {
  return shortcut ? shortcut.split("+").join(" + ") : "未设置";
}

function loadShortcuts() {
  try {
    chrome.commands.getAll((commands) => {
      const map = {};
      for (const command of commands || []) {
        map[command.name] = command.shortcut;
      }

      const items = [
        ["toggle-play", "播放 / 暂停"],
        ["seek-backward", "快退"],
        ["seek-forward", "快进"]
      ];

      shortcutList.replaceChildren();
      for (const [name, label] of items) {
        const row = document.createElement("div");
        row.className = "shortcut-item";

        const text = document.createElement("span");
        text.textContent = label;

        const key = document.createElement("kbd");
        const shortcut = map[name];
        key.textContent = formatShortcut(shortcut);
        if (!shortcut) key.className = "missing";

        row.append(text, key);
        shortcutList.append(row);
      }
    });
  } catch (_) {
    shortcutList.replaceChildren();
    const row = document.createElement("div");
    row.className = "shortcut-item";
    const text = document.createElement("span");
    text.textContent = "无法读取快捷键，请重新加载扩展。";
    row.append(text);
    shortcutList.append(row);
  }
}

function shortcutsUrl() {
  return /Edg\//.test(navigator.userAgent)
    ? "edge://extensions/shortcuts"
    : "chrome://extensions/shortcuts";
}

function openShortcuts() {
  const url = shortcutsUrl();
  try {
    chrome.tabs.create({ url }, () => {
      if (chrome.runtime.lastError) {
        navigator.clipboard
          .writeText(url)
          .then(() => showStatus("设置地址已复制，请粘贴到地址栏打开"))
          .catch(() => showStatus("请手动打开：" + url, true));
      }
    });
  } catch (_) {
    showStatus("请手动打开：" + url, true);
  }
}

document.querySelectorAll("[data-step]").forEach((button) => {
  button.addEventListener("click", () => {
    stepInput.value = button.dataset.step;
    updatePresets();
  });
});

stepInput.addEventListener("input", updatePresets);
stepInput.addEventListener("blur", () => {
  stepInput.value = String(normalizeStep(stepInput.value));
  updatePresets();
});

form.addEventListener("submit", saveSettings);
document.getElementById("open-shortcuts").addEventListener("click", openShortcuts);

try {
  versionLabel.textContent = "v" + chrome.runtime.getManifest().version;
} catch (_) {
  // Keep the fallback label.
}

loadSettings();
loadShortcuts();
