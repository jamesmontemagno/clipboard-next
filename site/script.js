function detectPlatform() {
  const agent = navigator.userAgent;
  const hints = navigator.userAgentData;
  const touchMac = navigator.maxTouchPoints > 1
    && (/Macintosh|Mac OS X/i.test(agent) || navigator.platform === "MacIntel");

  if (hints?.mobile || /Android|iPhone|iPad|iPod|Windows Phone/i.test(agent) || touchMac) {
    return null;
  }

  const agentPlatform = /Windows NT/i.test(agent) ? "windows"
    : /Macintosh|Mac OS X/i.test(agent) ? "macos"
    : /Linux|X11|CrOS/i.test(agent) ? "other" : null;

  if (hints?.platform) {
    const hintPlatform = hints.platform === "Windows" ? "windows"
      : hints.platform === "macOS" ? "macos" : null;
    return hintPlatform && (!agentPlatform || agentPlatform === hintPlatform) ? hintPlatform : null;
  }

  return agentPlatform === "other" ? null : agentPlatform;
}

const detectedPlatform = detectPlatform();
const platformSelect = document.getElementById("platform-select");

function renderPlatform() {
  const automatic = platformSelect.value === "auto";
  const platform = automatic ? detectedPlatform : platformSelect.value;
  const name = platform === "macos" ? "macOS" : "Windows";

  for (const element of document.querySelectorAll("[data-platform]")) {
    element.hidden = platform !== null && element.dataset.platform !== platform;
  }
  for (const element of document.querySelectorAll("[data-platform-label], [data-platform-separator]")) {
    element.hidden = platform !== null;
  }
  for (const link of document.querySelectorAll("[data-download-platform]")) {
    link.classList.toggle("primary", link.dataset.downloadPlatform === platform);
  }
  for (const card of document.querySelectorAll("[data-download-card]")) {
    card.classList.toggle("recommended", card.dataset.downloadCard === platform);
  }
  for (const label of document.querySelectorAll("[data-recommendation]")) {
    label.hidden = label.dataset.recommendation !== platform;
    label.textContent = automatic ? "Recommended for your device" : "Selected platform";
  }

  document.getElementById("platform-status").textContent = platform
    ? `Showing ${name} instructions${automatic ? " for your device" : ""}. Both downloads are available below.`
    : "Showing both platforms. Choose macOS or Windows to tailor the instructions.";
}

platformSelect.addEventListener("change", renderPlatform);
renderPlatform();
document.querySelector(".platform-picker").hidden = false;

const commands = [
  "maui devflow broker start",
  "adb reverse tcp:19223 tcp:19223",
  "maui devflow list",
  "maui devflow ui tree --depth 4",
  "maui devflow ui tap --automationId OpenSettings",
];

const term = document.getElementById("term");
const queue = document.getElementById("queue");
const badge = document.getElementById("badge");
const badgeCount = document.getElementById("badge-count");
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

for (const command of commands) {
  const item = document.createElement("li");
  item.textContent = command;
  queue.append(item);
}

function render(step) {
  [...queue.children].forEach((item, i) => {
    item.classList.toggle("done", i < step);
    item.classList.toggle("current", i === step);
  });
  badgeCount.textContent = step < commands.length ? `${step + 1}/${commands.length}` : "Done";
}

function appendLine(text) {
  const prompt = document.createElement("span");
  prompt.className = "prompt";
  prompt.textContent = "❯ ";
  const pasted = document.createElement("span");
  pasted.className = "flash";
  pasted.textContent = text;
  term.append(prompt, pasted, "\n");
  setTimeout(() => pasted.classList.remove("flash"), 500);
}

if (reduceMotion) {
  commands.forEach(appendLine);
  render(commands.length);
} else {
  let step = 0;
  render(0);
  setInterval(() => {
    if (step >= commands.length) {
      step = 0;
      term.textContent = "";
      render(0);
      return;
    }
    appendLine(commands[step]);
    badge.classList.add("hit");
    badge.querySelector(".badge-icon").textContent = "✓";
    setTimeout(() => {
      badge.classList.remove("hit");
      badge.querySelector(".badge-icon").textContent = "📋";
      step += 1;
      render(step);
    }, 450);
  }, 1700);
}

for (const button of document.querySelectorAll("[data-copy]")) {
  button.addEventListener("click", async () => {
    const label = button.querySelector(".copy-label");
    try {
      await navigator.clipboard.writeText(button.dataset.copy);
      button.classList.add("copied");
      label.textContent = "Copied";
    } catch {
      label.textContent = "Select & copy";
    }
    setTimeout(() => {
      button.classList.remove("copied");
      label.textContent = "Copy";
    }, 1600);
  });
}
