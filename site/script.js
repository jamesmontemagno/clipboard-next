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
