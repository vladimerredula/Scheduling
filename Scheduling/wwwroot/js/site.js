function showToast(message, type = "success", duration = 7000) {
    let toastContainer = $("#toastContainer"); // Ensure toast container exists

    if (!toastContainer.length) {
        $("body").append('<div id="toastContainer" class="toast-container position-fixed bottom-0 end-0 p-3"></div>');
        toastContainer = $("#toastContainer");
    }

    // Create a unique toast element
    let toast = $(`
        <div class="toast align-items-center text-bg-${type} border-0 shadow fade" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="${duration}">
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `);

    toastContainer.append(toast);

    // Initialize and show the toast
    let bsToast = new bootstrap.Toast(toast[0]);
    bsToast.show();

    // Automatically remove the toast after it disappears
    toast.on("hidden.bs.toast", function () {
        $(this).remove();
    });
}