(() => {
  const app = document.getElementById("viewer-app");
  if (!app) {
    return;
  }

  const wsUrl = app.dataset.wsUrl;
  const deviceId = app.dataset.deviceId;
  const shell = document.getElementById("streamShell");
  const image = document.getElementById("desktopStream");
  const dot = document.getElementById("connectionDot");
  const label = document.getElementById("connectionLabel");
  const focusButton = document.getElementById("focusRemoteButton");
  const fullscreenButton = document.getElementById("fullscreenButton");

  let socket = null;
  let lastMoveAt = 0;
  let lastObjectUrl = null;

  const setStatus = (connected, text) => {
    dot.classList.toggle("connected", connected);
    label.textContent = text;
  };

  const getRelativePoint = (event) => {
    const rect = image.getBoundingClientRect();
    if (!rect.width || !rect.height) {
      return null;
    }

    const x = (event.clientX - rect.left) / rect.width;
    const y = (event.clientY - rect.top) / rect.height;
    return { x: Math.min(Math.max(x, 0), 1), y: Math.min(Math.max(y, 0), 1) };
  };

  const mapButton = (event) => {
    switch (event.button) {
      case 1: return "middle";
      case 2: return "right";
      default: return "left";
    }
  };

  const send = (payload) => {
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      return;
    }
    socket.send(JSON.stringify(payload));
  };

  const connect = () => {
    setStatus(false, `裝置 ${deviceId} 連線中`);
    socket = new WebSocket(wsUrl);
    socket.binaryType = "blob";

    socket.addEventListener("open", () => setStatus(true, "已連線，等待畫面"));
    socket.addEventListener("message", (event) => {
      if (typeof event.data === "string") {
        const payload = JSON.parse(event.data);
        if (payload.type === "viewer-ready") {
          setStatus(true, `控制中: ${payload.deviceName}`);
        }
        return;
      }

      if (lastObjectUrl) {
        URL.revokeObjectURL(lastObjectUrl);
      }
      lastObjectUrl = URL.createObjectURL(event.data);
      image.src = lastObjectUrl;
      setStatus(true, "畫面更新中");
    });
    socket.addEventListener("close", () => setStatus(false, "裝置離線或目前被其他控制者占用"));
    socket.addEventListener("error", () => setStatus(false, "WebSocket 連線失敗"));
  };

  focusButton?.addEventListener("click", () => shell.focus());
  fullscreenButton?.addEventListener("click", async () => {
    if (document.fullscreenElement) {
      await document.exitFullscreen();
    } else {
      await shell.requestFullscreen();
    }
  });

  shell.addEventListener("contextmenu", (event) => event.preventDefault());
  shell.addEventListener("mousedown", (event) => {
    const point = getRelativePoint(event);
    if (!point) {
      return;
    }
    shell.focus();
    send({ type: "mousedown", x: point.x, y: point.y, button: mapButton(event) });
  });

  window.addEventListener("mouseup", (event) => {
    const point = getRelativePoint(event) ?? { x: 0.5, y: 0.5 };
    send({ type: "mouseup", x: point.x, y: point.y, button: mapButton(event) });
  });

  shell.addEventListener("mousemove", (event) => {
    const now = performance.now();
    if (now - lastMoveAt < 33) {
      return;
    }
    const point = getRelativePoint(event);
    if (!point) {
      return;
    }
    lastMoveAt = now;
    send({ type: "move", x: point.x, y: point.y });
  });

  shell.addEventListener("wheel", (event) => {
    event.preventDefault();
    const point = getRelativePoint(event) ?? { x: 0.5, y: 0.5 };
    send({ type: "wheel", x: point.x, y: point.y, deltaY: event.deltaY });
  }, { passive: false });

  shell.addEventListener("keydown", (event) => {
    const printable = event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey;
    event.preventDefault();
    if (printable) {
      send({ type: "text", key: event.key });
      return;
    }
    send({ type: "keydown", key: event.key, code: event.code });
  });

  shell.addEventListener("keyup", (event) => {
    if (event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
      return;
    }
    event.preventDefault();
    send({ type: "keyup", key: event.key, code: event.code });
  });

  connect();
})();
