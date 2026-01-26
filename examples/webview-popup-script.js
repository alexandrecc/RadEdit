const button = document.getElementById('openPopup');
const status = document.getElementById('statusMain');

if (button) {
    button.addEventListener('click', () => {
        window.open('webview-popup-child.html', 'radedit-popup', 'width=640,height=480');
    });
}

if (status) {
    status.textContent = `Main script loaded at ${new Date().toLocaleTimeString()}`;
}
