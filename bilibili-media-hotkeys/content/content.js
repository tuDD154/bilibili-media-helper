"use strict";

(function () {
  if (globalThis.__BILI_MEDIA_HOTKEYS_CONTENT__) return;

  const core = globalThis.BiliMediaCore;
  if (!core) return;

  globalThis.__BILI_MEDIA_HOTKEYS_CONTENT__ = true;

  const TOAST_ID = "__bili_media_hotkeys_toast__";
  let toastTimer = null;

  function toastHost() {
    const fullscreen = document.fullscreenElement;
    if (fullscreen && fullscreen.tagName !== "VIDEO") return fullscreen;
    return document.body || document.documentElement;
  }

  function showToast(text, isError) {
    try {
      const host = toastHost();
      let toast = document.getElementById(TOAST_ID);

      if (!toast) {
        toast = document.createElement("div");
        toast.id = TOAST_ID;
        toast.setAttribute("role", "status");
        toast.setAttribute("aria-live", "polite");
        Object.assign(toast.style, {
          position: "fixed",
          top: "18px",
          left: "50%",
          transform: "translateX(-50%) translateY(-6px)",
          zIndex: "2147483647",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: "9px 16px",
          borderRadius: "999px",
          background: "rgba(17, 20, 28, 0.9)",
          color: "#ffffff",
          font: "600 14px/1.2 -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Microsoft YaHei', sans-serif",
          letterSpacing: "0.2px",
          whiteSpace: "nowrap",
          boxShadow: "0 8px 28px rgba(0, 0, 0, 0.35)",
          border: "1px solid rgba(255, 255, 255, 0.14)",
          backdropFilter: "blur(8px)",
          pointerEvents: "none",
          userSelect: "none",
          opacity: "0",
          transition: "opacity 160ms ease, transform 160ms ease"
        });
        host.appendChild(toast);
      } else if (toast.parentNode !== host) {
        host.appendChild(toast);
      }

      toast.textContent = text;
      toast.style.background = isError ? "rgba(150, 30, 45, 0.94)" : "rgba(17, 20, 28, 0.9)";
      void toast.offsetWidth;
      toast.style.opacity = "1";
      toast.style.transform = "translateX(-50%) translateY(0)";

      if (toastTimer) clearTimeout(toastTimer);
      toastTimer = setTimeout(() => {
        toast.style.opacity = "0";
        toast.style.transform = "translateX(-50%) translateY(-6px)";
        toastTimer = null;
      }, 1100);
    } catch (_) {
      // The toast is a convenience only.
    }
  }

  function normalizeStep(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return 3;
    return Math.min(30, Math.max(1, Math.round(number)));
  }

  function finish(sendResponse, result, action, step, shouldToast) {
    if (result && result.ok) {
      if (shouldToast) {
        if (action === "toggle") {
          showToast(result.state && result.state.paused ? "⏸ 已暂停" : "▶ 正在播放");
        } else if (action === "back") {
          showToast("⏪ 快退 " + step + " 秒");
        } else {
          showToast("⏩ 快进 " + step + " 秒");
        }
      }
    } else if (result && result.reason === "play-blocked" && shouldToast) {
      showToast("请先在B站页面点一次播放", true);
    }

    try {
      sendResponse(result);
    } catch (_) {
      // The message port can close if the service worker was suspended.
    }
  }

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message || typeof message !== "object") return undefined;
    if (sender && sender.id && sender.id !== chrome.runtime.id) return undefined;

    if (message.type === "BILI_MEDIA_GET_STATE") {
      let video = null;
      try {
        video = core.findVideo();
      } catch (_) {
        video = null;
      }
      if (!video) {
        sendResponse({ ok: false, reason: "no-video" });
        return undefined;
      }
      try {
        sendResponse({ ok: true, state: core.getState(video) });
      } catch (error) {
        sendResponse({ ok: false, reason: "state-error", error: String(error) });
      }
      return undefined;
    }

    if (message.type !== "BILI_MEDIA_COMMAND") return undefined;
    if (!["toggle", "back", "forward"].includes(message.action)) {
      sendResponse({ ok: false, reason: "bad-action" });
      return undefined;
    }

    let video = null;
    try {
      video = core.findVideo();
    } catch (_) {
      video = null;
    }
    if (!video) {
      sendResponse({ ok: false, reason: "no-video" });
      return undefined;
    }

    const action = message.action;
    const step = normalizeStep(message.step);
    const shouldToast = message.showToast !== false;

    if (action === "toggle") {
      core
        .toggle(video)
        .then((result) => finish(sendResponse, result, action, step, shouldToast))
        .catch((error) =>
          finish(
            sendResponse,
            { ok: false, reason: "error", error: String(error) },
            action,
            step,
            shouldToast
          )
        );
      return true;
    }

    const delta = action === "back" ? -step : step;
    core
      .seekBy(video, delta)
      .then((result) => finish(sendResponse, result, action, step, shouldToast))
      .catch((error) =>
        finish(
          sendResponse,
          { ok: false, reason: "error", error: String(error) },
          action,
          step,
          shouldToast
        )
      );
    return true;
  });
})();
