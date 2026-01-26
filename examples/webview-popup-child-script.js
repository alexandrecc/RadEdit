const status = document.getElementById('statusPopup');
const locationEl = document.getElementById('locationPopup');

if (status) {
    status.textContent = `Popup script loaded at ${new Date().toLocaleTimeString()}`;
}

if (locationEl) {
    locationEl.textContent = window.location.href;
}
