(function () {
    "use strict";

    if (window.__artifyContentProtectionLoaded) return;
    window.__artifyContentProtectionLoaded = true;

    var editableSelector = "input, textarea, select, [contenteditable='true']";
    var guardTimer = null;
    var allowClipboardWrite = false;

    document.documentElement.classList.add("artify-protection-active");

    function isEditable(target) {
        return !!(target && target.closest && target.closest(editableSelector));
    }

    function ensureGuard() {
        var guard = document.getElementById("artifyScreenGuard");
        if (guard) return guard;

        guard = document.createElement("div");
        guard.id = "artifyScreenGuard";
        guard.className = "artify-screen-guard";
        guard.setAttribute("aria-hidden", "true");
        guard.innerHTML =
            '<div class="artify-screen-guard__panel">' +
                '<p class="artify-screen-guard__title">Protected artwork</p>' +
                '<p class="artify-screen-guard__text">Copying, screenshots, dragging, printing, and inspection shortcuts are disabled on this page.</p>' +
            '</div>';
        document.body.appendChild(guard);
        return guard;
    }

    function showGuard(duration, blackOnly) {
        var guard = ensureGuard();
        guard.classList.toggle("is-blackout", !!blackOnly);
        guard.classList.add("is-visible");
        guard.setAttribute("aria-hidden", "false");

        window.clearTimeout(guardTimer);
        guardTimer = window.setTimeout(function () {
            guard.classList.remove("is-visible");
            guard.setAttribute("aria-hidden", "true");
        }, duration || 1600);
    }

    function hardenMedia() {
        document.querySelectorAll("img, picture, canvas, video").forEach(function (node) {
            node.setAttribute("draggable", "false");
            node.addEventListener("dragstart", blockEvent, true);
            node.addEventListener("contextmenu", blockEvent, true);
        });

        document.querySelectorAll("a").forEach(function (node) {
            node.setAttribute("draggable", "false");
        });
    }

    function makeBlackCanvasBlob(callback) {
        var canvas = document.createElement("canvas");
        canvas.width = Math.max(1, window.innerWidth || screen.width || 1920);
        canvas.height = Math.max(1, window.innerHeight || screen.height || 1080);

        var ctx = canvas.getContext("2d");
        ctx.fillStyle = "#000";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        if (canvas.toBlob) {
            canvas.toBlob(function (blob) {
                callback(blob);
            }, "image/png");
            return;
        }

        callback(null);
    }

    function fallbackBlackClipboardWrite() {
        var cleaner = document.createElement("textarea");
        cleaner.value = "BLACK SCREEN";
        cleaner.setAttribute("readonly", "");
        cleaner.style.position = "fixed";
        cleaner.style.top = "-1000px";
        cleaner.style.left = "-1000px";
        cleaner.style.opacity = "0";
        document.body.appendChild(cleaner);

        function copyBlackPayload(event) {
            if (!allowClipboardWrite || !event.clipboardData) return;

            event.preventDefault();
            event.clipboardData.setData("text/plain", "BLACK SCREEN");
            event.clipboardData.setData("text/html", '<div style="width:100vw;height:100vh;background:#000;color:#000;">BLACK SCREEN</div>');
        }

        document.addEventListener("copy", copyBlackPayload, true);

        try {
            allowClipboardWrite = true;
            cleaner.focus();
            cleaner.select();
            cleaner.setSelectionRange(0, cleaner.value.length);
            document.execCommand("copy");
        } catch (err) {
        } finally {
            allowClipboardWrite = false;
            document.removeEventListener("copy", copyBlackPayload, true);
            cleaner.remove();
        }
    }

    function writeBlackClipboard() {
        if (
            navigator.clipboard &&
            window.ClipboardItem &&
            typeof navigator.clipboard.write === "function" &&
            window.isSecureContext
        ) {
            makeBlackCanvasBlob(function (blob) {
                if (!blob) {
                    fallbackBlackClipboardWrite();
                    return;
                }

                navigator.clipboard.write([
                    new ClipboardItem({
                        "image/png": blob,
                        "text/plain": new Blob(["BLACK SCREEN"], { type: "text/plain" })
                    })
                ]).catch(fallbackBlackClipboardWrite);
            });
            return;
        }

        fallbackBlackClipboardWrite();
    }

    function scheduleBlackClipboardWrites(duration) {
        var delays = [0, 80, 180, 350, 700, 1200, 2000, 3200, 5000, 8000];
        var maxDuration = typeof duration === "number" ? duration : 8500;

        delays.forEach(function (delay) {
            if (delay <= maxDuration) {
                window.setTimeout(writeBlackClipboard, delay);
            }
        });
    }

    function blockEvent(event) {
        if (event && isEditable(event.target)) return;
        if (event) {
            event.preventDefault();
            event.stopPropagation();
        }
        return false;
    }

    function isBlockedShortcut(event) {
        var key = String(event.key || "").toLowerCase();

        if (key === "f12" || key === "printscreen") return true;

        if (event.ctrlKey && event.shiftKey && ["i", "j", "c", "k"].indexOf(key) !== -1) {
            return true;
        }

        if ((event.ctrlKey || event.metaKey) && ["u", "s", "p"].indexOf(key) !== -1) {
            return true;
        }

        return false;
    }

    function isScreenshotShortcut(event) {
        var key = String(event.key || "").toLowerCase();
        var code = String(event.code || "").toLowerCase();
        var ctrlOrMeta = event.ctrlKey || event.metaKey;

        return (
            key === "printscreen" ||
            code === "printscreen" ||
            (event.shiftKey && (key === "s" || code === "keys")) ||
            (ctrlOrMeta && event.shiftKey && (key === "s" || code === "keys"))
        );
    }

    document.addEventListener("contextmenu", blockEvent, true);
    document.addEventListener("selectstart", function (event) {
        if (!isEditable(event.target)) blockEvent(event);
    }, true);
    document.addEventListener("copy", function (event) {
        if (!allowClipboardWrite) blockEvent(event);
    }, true);
    document.addEventListener("cut", blockEvent, true);

    document.addEventListener("dragstart", function (event) {
        if (event.target && event.target.closest && event.target.closest("img, picture, canvas, video, a")) {
            blockEvent(event);
        }
    }, true);

    document.addEventListener("drop", function (event) {
        if (event.dataTransfer && Array.prototype.indexOf.call(event.dataTransfer.types || [], "Files") !== -1) {
            return;
        }
        blockEvent(event);
    }, true);

    document.addEventListener("keydown", function (event) {
        var screenshotShortcut = isScreenshotShortcut(event);
        if (!isBlockedShortcut(event) && !screenshotShortcut) return;

        blockEvent(event);
        showGuard(screenshotShortcut ? 8500 : 1400, screenshotShortcut);

        if (screenshotShortcut) {
            scheduleBlackClipboardWrites(8500);
        }
    }, true);

    document.addEventListener("keyup", function (event) {
        if (!isScreenshotShortcut(event)) return;

        blockEvent(event);
        showGuard(8500, true);
        scheduleBlackClipboardWrites(8500);
    }, true);

    window.addEventListener("beforeprint", function (event) {
        blockEvent(event);
        showGuard(8500, true);
        scheduleBlackClipboardWrites(8500);
    });

    document.addEventListener("visibilitychange", function () {
        if (document.hidden) {
            showGuard(8500, true);
            scheduleBlackClipboardWrites(8500);
        }
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", hardenMedia);
    } else {
        hardenMedia();
    }

    new MutationObserver(hardenMedia).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
