$(document).ready(function () {
    getRequestCount();
    getNotifications();
});

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

function getRequestCount() {
    $.ajax({
        url: '/Home/GetRequestCount',
        type: 'POST',
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function (count) {
            $("#requestCount").text(count);
        },
        error: function (response) {
            console.log(response);
        }
    });
}

function getNotifications() {
    $.post("/Home/GetNotifications", function (notifications) {
        var notificationList = $("#notificationList").empty();

        notificationList.append('<li><h6 class="dropdown-header">Notifications</h6></li>');

        var newNotifCount = 0;

        if (notifications.length > 0) {
            $.each(notifications, function (key, notification) {
                var newNotif = notification["notify"] == 1;

                var item = $("<li>").addClass("border-top notif")
                    .attr("id", notification["leave_ID"])
                    .attr("data-link", "/Leave/Index#" + notification["status"].toLowerCase());
                var button = $("<a>").addClass("dropdown-item py-2").attr("href", "#");
                var div = $("<div>").addClass("d-flex w-100 justify-content-between");
                var h6 = $("<h6>").addClass("mt-1 mb-0");

                if (newNotif) {
                    newNotifCount++;
                    h6.append($("<span>").addClass("badge rounded-pill bg-primary me-1 border border-light").text("New"));
                } else if (notification["notify"] == 3) {
                    button.addClass("text-secondary");
                }

                let dateApproved1 = new Date(notification["date_approved_1"]);
                let dateApproved2 = new Date(notification["date_approved_2"]);
                let dateApproved = dateApproved1 > dateApproved2 ? dateApproved1 : dateApproved2;
                let dateNow = new Date();
                let dateDiff = dateNow - dateApproved;

                let seconds = Math.floor(dateDiff / 1000);
                let minutes = Math.floor(seconds / 60);
                let hours = Math.floor(minutes / 60);
                let days = Math.floor(hours / 24);

                var notifDate = "Just now";

                if (days > 0) {
                    notifDate = `${days} day${days > 1 ? 's' : ''} ago`;
                } else if (hours > 0) {
                    notifDate = `${hours} hour${hours > 1 ? 's' : ''} ago`;
                } else if (minutes > 0) {
                    notifDate = `${minutes} minute${minutes > 1 ? 's' : ''} ago`;
                }

                h6.append("Leave request");
                div.append(h6);
                var small = $("<small>").text(notifDate);
                div.append(small);
                button.append(div);

                let leaveType = "";
                switch (notification["leave_type_ID"]) {
                    case 10:
                        leaveType = "Business trip";
                        break;
                    case 11:
                        leaveType = "Paid leave";
                        break;
                    case 12:
                        leaveType = "Unpaid leave";
                        break;
                    default:
                        leaveType = "Leave";
                        break;
                }

                var status = notification["status"];
                small = $("<small>").text(`${leaveType} request has been ${status}.`);
                button.append(small);
                item.append(button);
                notificationList.append(item);
            });
        } else {
            notificationList.append('<li><h5 class="dropdown-item mb-1 border-top text-center disabled">No notifications</h5></li>');
        }

        $("#notificationCount").prop("hidden", newNotifCount == 0);
    });
}

$("#notifDropdown").on('hidden.bs.dropdown', function () {
    $.ajax({
        url: '/Home/UpdateNotifications',
        type: 'POST',
        success: function (response) {
            getNotifications();
        },
        error: function (response) {
            console.log(response);
        }
    });
});

$(document).on("click", ".notif", function () {
    let id = this.id;
    let url = $(this).data("link");
    $.ajax({
        url: "/Home/ReadNotification",
        type: "POST",
        data: { id: id },
        success: function (data) {
            window.location.href = url;
        },
        error: function (response) {
            console.error("Error:", response);
        }
    });
});