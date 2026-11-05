document.querySelectorAll(".alert.alert-success").forEach((notification) => {
    window.setTimeout(() => notification.remove(), 5000);
});
