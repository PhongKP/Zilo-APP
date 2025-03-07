// Thiết lập kết nối SignalR
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/FriendsHub")
    .build();

// Tạo một đối tượng toastr tạm thời nếu chưa được định nghĩa
if (typeof toastr === 'undefined') {
    console.warn("Thư viện toastr không được tìm thấy, sử dụng phiên bản tạm thời");

    // Tạo đối tượng toastr giả
    window.toastr = {
        options: {
            "closeButton": true,
            "positionClass": "toast-top-right",
            "timeOut": "3000"
        },

        // Hàm hiển thị thông báo
        _showNotification: function (message, type) {
            // Tạo phần tử thông báo
            const toast = document.createElement("div");
            toast.style.position = "fixed";
            toast.style.top = "20px";
            toast.style.right = "20px";
            toast.style.padding = "12px 20px";
            toast.style.borderRadius = "4px";
            toast.style.color = "white";
            toast.style.zIndex = "9999";
            toast.style.minWidth = "250px";
            toast.style.boxShadow = "0 3px 10px rgba(0,0,0,0.2)";

            // Đặt màu dựa trên loại thông báo
            switch (type) {
                case 'success': toast.style.backgroundColor = "#28a745"; break;
                case 'error': toast.style.backgroundColor = "#dc3545"; break;
                case 'warning': toast.style.backgroundColor = "#ffc107"; toast.style.color = "#333"; break;
                default: toast.style.backgroundColor = "#17a2b8"; // info
            }

            toast.textContent = message;
            document.body.appendChild(toast);

            // Tự động xóa sau 3 giây
            setTimeout(() => {
                toast.style.opacity = "0";
                toast.style.transition = "opacity 0.5s ease";
                setTimeout(() => toast.remove(), 500);
            }, 3000);
        },

        // Các phương thức cho các loại thông báo khác nhau
        success: function (message) { this._showNotification(message, 'success'); },
        error: function (message) { this._showNotification(message, 'error'); },
        warning: function (message) { this._showNotification(message, 'warning'); },
        info: function (message) { this._showNotification(message, 'info'); }
    };
}

// Biến lưu trữ dữ liệu
let currentFriends = [];
let pendingRequests = [];
let sentRequests = [];
let currentUserId = '';
let unfriendId = '';

// Hàm khởi tạo
$(document).ready(function () {
    // Khởi tạo kết nối SignalR
    connection.start()
        .then(() => console.log('SignalR Connected'))
        .catch(err => console.error('SignalR Connection Error: ', err));

    // Khởi tạo dữ liệu ban đầu
    loadFriends();
    loadPendingRequests();
    loadSentRequests();

    // Xử lý sự kiện chuyển tab
    $('.tab-btn').click(function () {
        const tabId = $(this).data('tab');
        $('.tab-btn').removeClass('active');
        $(this).addClass('active');
        $('.tab-content').hide();
        $(`#${tabId}-content`).show();
    });

    // Xử lý sự kiện chuyển tab lời mời
    $('.request-tab-btn').click(function () {
        const tabId = $(this).data('request-tab');
        $('.request-tab-btn').removeClass('active');
        $(this).addClass('active');
        $('.request-content').hide();
        $(`#${tabId}-requests`).show();
    });

    // Xử lý tìm kiếm bạn bè
    $('#friend-search').on('input', function () {
        const searchTerm = $(this).val().toLowerCase();
        filterFriends(searchTerm);
    });

    // Xử lý tìm kiếm người dùng
    let searchTimeout;
    $('#user-search').on('input', function () {
        clearTimeout(searchTimeout);
        const searchTerm = $(this).val();

        if (searchTerm.length < 2) {
            $('#search-results').html(`
                <div class="empty-search">
                    <i class="bi bi-search"></i>
                    <p>Nhập tên hoặc email để tìm kiếm</p>
                </div>
            `);
            return;
        }

        $('#search-results').html(`
            <div class="search-loading">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Đang tìm kiếm...</span>
                </div>
            </div>
        `);

        searchTimeout = setTimeout(() => {
            searchUsers(searchTerm);
        }, 500);
    });

    // Xử lý sự kiện unfriend
    $(document).on('click', '.btn-unfriend', function () {
        const friendId = $(this).data('id');
        const friendName = $(this).data('name');
        unfriendId = friendId;
        $('#unfriend-name').text(friendName);
        $('#unfriendModal').modal('show');
    });

    // Xác nhận unfriend
    $('#confirm-unfriend-btn').click(function () {
        if (unfriendId) {
            unfriend(unfriendId);
            $('#unfriendModal').modal('hide');
        }
    });

    // Xử lý sự kiện chấp nhận lời mời kết bạn
    $(document).on('click', '.btn-accept', function () {
        const requestId = $(this).data('id');
        acceptFriendRequest(requestId);
    });

    // Xử lý sự kiện từ chối lời mời kết bạn
    $(document).on('click', '.btn-decline', function () {
        const requestId = $(this).data('id');
        declineFriendRequest(requestId);
    });

    // Xử lý sự kiện hủy lời mời kết bạn đã gửi
    $(document).on('click', '.btn-cancel', function () {
        const requestId = $(this).data('id');
        cancelFriendRequest(requestId);
    });

    // Xử lý sự kiện gửi lời mời kết bạn
    $(document).on('click', '.btn-add', function () {
        const userId = $(this).data('id');
        sendFriendRequest(userId);
    });

    // Xử lý sự kiện bắt đầu chat với bạn bè
    $(document).on('click', '.btn-chat', function () {
        const friendId = $(this).data('id');
        window.location.href = `/Chat?userId=${friendId}`;
    });
});

// Xử lý sự kiện SignalR
connection.on("ReceiveFriendRequest", (senderId) => {
    // Tải lại danh sách lời mời kết bạn
    loadPendingRequests();
    // Hiển thị thông báo
    toastr.info('Bạn có lời mời kết bạn mới');
    updateRequestBadge();
});

connection.on("ReceiveFriendRequestAccepted", (userId) => {
    // Tải lại danh sách bạn bè và lời mời đã gửi
    loadFriends();
    loadSentRequests();
    // Hiển thị thông báo
    toastr.success('Lời mời kết bạn của bạn đã được chấp nhận');
});

// Hàm tải danh sách bạn bè
function loadFriends() {
    $('#friends-list').html(`
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Đang tải...</span>
            </div>
        </div>
    `);

    $.ajax({
        url: '/Friends/GetFriends',
        type: 'GET',
        success: function (response) {
            currentFriends = response;
            renderFriends(response);
        },
        error: function (error) {
            console.error('Lỗi khi tải danh sách bạn bè:', error);
            $('#friends-list').html(`
                <div class="alert alert-danger">
                    Có lỗi xảy ra khi tải danh sách bạn bè.
                </div>
            `);
        }
    });
}

// Hàm tải danh sách lời mời kết bạn đã nhận
function loadPendingRequests() {
    $('#received-requests').html(`
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Đang tải...</span>
            </div>
        </div>
    `);

    $.ajax({
        url: '/Friends/GetPendingRequests',
        type: 'GET',
        success: function (response) {
            console.log(`Pending Request: {v} `, response);
            pendingRequests = response;
            renderPendingRequests(response);
            updateRequestBadge();
        },
        error: function (error) {
            console.error('Lỗi khi tải lời mời kết bạn:', error);
            $('#received-requests').html(`
                <div class="alert alert-danger">
                    Có lỗi xảy ra khi tải lời mời kết bạn.
                </div>
            `);
        }
    });
}

// Hàm tải danh sách lời mời kết bạn đã gửi
function loadSentRequests() {
    $('#sent-requests').html(`
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Đang tải...</span>
            </div>
        </div>
    `);

    $.ajax({
        url: '/Friends/GetSentRequests',
        type: 'GET',
        success: function (response) {
            console.log(`Sending Request: `, response);
            sentRequests = response;
            renderSentRequests(response);
        },
        error: function (error) {
            console.error('Lỗi khi tải lời mời đã gửi:', error);
            $('#sent-requests').html(`
                <div class="alert alert-danger">
                    Có lỗi xảy ra khi tải lời mời đã gửi.
                </div>
            `);
        }
    });
}

// Hàm hiển thị danh sách bạn bè
function renderFriends(friends) {
    if (!friends || friends.length === 0) {
        $('#friends-list').html(`
            <div class="alert alert-info">
                <p class="mb-0">Bạn chưa có bạn bè nào.</p>
            </div>
        `);
        return;
    }

    let html = '';
    friends.forEach(friend => {
        const firstLetter = friend.fullName ? friend.fullName.charAt(0) : friend.email.charAt(0);
        const displayName = friend.fullName || friend.email;

        html += `
            <div class="friend-card">
                <div class="friend-info">
                    <div class="friend-avatar">${firstLetter.toUpperCase()}</div>
                    <div class="friend-details">
                        <h4>${displayName}</h4>
                        <p>${friend.email}</p>
                    </div>
                </div>
                <div class="friend-actions">
                    <button class="friend-btn btn-chat" data-id="${friend.id}">
                        <i class="bi bi-chat-fill"></i> Chat
                    </button>
                    <button class="friend-btn btn-unfriend" data-id="${friend.id}" data-name="${displayName}">
                        <i class="bi bi-person-dash-fill"></i> Hủy kết bạn
                    </button>
                </div>
            </div>
        `;
    });

    $('#friends-list').html(html);
}

// Hàm hiển thị lời mời kết bạn đã nhận
function renderPendingRequests(requests) {
    if (!requests || requests.length === 0) {
        $('#received-requests').html(`
            <div class="alert alert-info">
                <p class="mb-0">Không có lời mời kết bạn nào.</p>
            </div>
        `);
        return;
    }

    let html = '';
    requests.forEach(request => {
        const sender = request.sender;
        const firstLetter = sender.fullName ? sender.fullName.charAt(0) : sender.email.charAt(0);
        const displayName = sender.fullName || sender.email;

        html += `
            <div class="request-card" data-id="${request.id}">
                <div class="friend-info">
                    <div class="friend-avatar">${firstLetter.toUpperCase()}</div>
                    <div class="friend-details">
                        <h4>${displayName}</h4>
                        <p>${sender.email}</p>
                    </div>
                </div>
                <div class="request-actions">
                    <button class="friend-btn btn-accept" data-id="${request.id}">
                        <i class="bi bi-check-lg"></i> Chấp nhận
                    </button>
                    <button class="friend-btn btn-decline" data-id="${request.id}">
                        <i class="bi bi-x-lg"></i> Từ chối
                    </button>
                </div>
            </div>
        `;
    });

    $('#received-requests').html(html);
}

// Hàm hiển thị lời mời kết bạn đã gửi
function renderSentRequests(requests) {
    if (!requests || requests.length === 0) {
        $('#sent-requests').html(`
            <div class="alert alert-info">
                <p class="mb-0">Bạn chưa gửi lời mời kết bạn nào.</p>
            </div>
        `);
        return;
    }

    let html = '';
    requests.forEach(request => {
        //console.log(request);
        const receiver = request.receiver;
        const firstLetter = receiver.fullName ? receiver.fullName.charAt(0) : receiver.email.charAt(0);
        const displayName = receiver.fullName || receiver.email;

        html += `
            <div class="request-card" data-id="${request.id}">
                <div class="friend-info">
                    <div class="friend-avatar">${firstLetter.toUpperCase()}</div>
                    <div class="friend-details">
                        <h4>${displayName}</h4>
                        <p>${receiver.email}</p>
                        <small class="text-muted">Đã gửi: ${formatDate(request.requestedAt)}</small>
                    </div>
                </div>
                <div class="request-actions">
                    <button class="friend-btn btn-cancel" data-id="${request.id}">
                        <i class="bi bi-x-lg"></i> Hủy
                    </button>
                </div>
            </div>
        `;
    });

    $('#sent-requests').html(html);
}

// Hàm tìm kiếm người dùng
function searchUsers(keyword) {
    $.ajax({
        url: '/Friends/SearchFriends',
        type: 'GET',
        data: { keyword: keyword },
        success: function (response) {
            renderSearchResults(response);
        },
        error: function (error) {
            console.error('Lỗi khi tìm kiếm người dùng:', error);
            $('#search-results').html(`
                <div class="alert alert-danger">
                    Có lỗi xảy ra khi tìm kiếm người dùng.
                </div>
            `);
        }
    });
}

// Hàm hiển thị kết quả tìm kiếm người dùng
function renderSearchResults(users) {
    if (!users || users.length === 0) {
        $('#search-results').html(`
            <div class="search-no-results">
                <p>Không tìm thấy người dùng nào khớp với từ khóa.</p>
            </div>
        `);
        return;
    }

    let html = '';
    users.forEach(user => {
        const firstLetter = user.name ? user.name.charAt(0) : user.email.charAt(0);
        const displayName = user.name || user.email;

        let actionButton = '';

        switch (user.status) {
            case 'NotFriends':
                actionButton = `
                    <button class="friend-btn btn-add" data-id="${user.id}">
                        <i class="bi bi-person-plus-fill"></i> Kết bạn
                    </button>
                `;
                break;
            case 'RequestSent':
                actionButton = `
                    <button class="friend-btn btn-pending" disabled>
                        <i class="bi bi-clock-fill"></i> Đã gửi lời mời
                    </button>
                `;
                break;
            case 'RequestReceived':
                actionButton = `
                    <button class="friend-btn btn-accept-inline" data-id="${user.id}">
                        <i class="bi bi-check-lg"></i> Chấp nhận
                    </button>
                    <button class="friend-btn btn-decline-inline" data-id="${user.id}">
                        <i class="bi bi-x-lg"></i> Từ chối
                    </button>
                `;
                break;
            case 'Friends':
                actionButton = `
                    <button class="friend-btn btn-chat" data-id="${user.id}">
                        <i class="bi bi-chat-fill"></i> Chat
                    </button>
                    <button class="friend-btn btn-unfriend" data-id="${user.id}" data-name="${displayName}">
                        <i class="bi bi-person-dash-fill"></i> Hủy kết bạn
                    </button>
                `;
                break;
        }
        html += `
            <div class="user-card">
                <div class="friend-info">
                    <div class="friend-avatar">${firstLetter.toUpperCase()}</div>
                    <div class="friend-details">
                        <h4>${displayName}</h4>
                        <p>${user.email}</p>
                    </div>
                </div>
                <div class="friend-actions">
                    ${actionButton}
                </div>
            </div>
        `;
    });

    $('#search-results').html(html);
}

// Hàm lọc danh sách bạn bè theo từ khóa
function filterFriends(searchTerm) {
    if (!searchTerm) {
        renderFriends(currentFriends);
        return;
    }

    const filtered = currentFriends.filter(friend => {
        const name = (friend.fullName || '').toLowerCase();
        const email = (friend.email || '').toLowerCase();
        return name.includes(searchTerm) || email.includes(searchTerm);
    });

    renderFriends(filtered);
}


// Hàm gửi lời mời kết bạn
function sendFriendRequest(receiverId) {
    $.ajax({
        url: '/Friends/SendFriendRequest',
        type: 'POST',
        data: { receiverId: receiverId },
        success: function (response) {
            if (response.success) {
                toastr.success('Đã gửi lời mời kết bạn');
                // Cập nhật giao diện
                searchUsers($('#user-search').val());
                loadSentRequests();
            } else {
                toastr.error('Không thể gửi lời mời kết bạn');
            }
        },
        error: function (error) {
            console.error('Lỗi khi gửi lời mời kết bạn:', error);
            toastr.error('Đã xảy ra lỗi khi gửi lời mời kết bạn');
        }
    });
}

// Hàm chấp nhận lời mời kết bạn
function acceptFriendRequest(requestId) {
    $.ajax({
        url: '/Friends/AcceptFriendRequest',
        type: 'POST',
        data: { requestId: requestId },
        success: function (response) {
            if (response.success) {
                toastr.success('Đã chấp nhận lời mời kết bạn');
                // Cập nhật giao diện
                loadFriends();
                loadPendingRequests();
            } else {
                toastr.error('Không thể chấp nhận lời mời kết bạn');
            }
        },
        error: function (error) {
            console.error('Lỗi khi chấp nhận lời mời kết bạn:', error);
            toastr.error('Đã xảy ra lỗi khi chấp nhận lời mời kết bạn');
        }
    });
}

// Hàm từ chối lời mời kết bạn
function declineFriendRequest(requestId) {
    $.ajax({
        url: '/Friends/DeclineFriendRequest',
        type: 'POST',
        data: { requestId: requestId },
        success: function (response) {
            if (response.success) {
                toastr.success('Đã từ chối lời mời kết bạn');
                // Cập nhật giao diện
                loadPendingRequests();
            } else {
                toastr.error('Không thể từ chối lời mời kết bạn');
            }
        },
        error: function (error) {
            console.error('Lỗi khi từ chối lời mời kết bạn:', error);
            toastr.error('Đã xảy ra lỗi khi từ chối lời mời kết bạn');
        }
    });
}

// Hàm hủy lời mời kết bạn đã gửi
function cancelFriendRequest(requestId) {
    $.ajax({
        url: '/Friends/CancelFriendRequest',
        type: 'POST',
        data: { requestId: requestId },
        success: function (response) {
            if (response.success) {
                toastr.success('Đã hủy lời mời kết bạn');
                // Cập nhật giao diện
                loadSentRequests();
                // Nếu đang ở tab tìm kiếm, cập nhật kết quả tìm kiếm
                if ($('#user-search').val()) {
                    searchUsers($('#user-search').val());
                }
            } else {
                toastr.error('Không thể hủy lời mời kết bạn');
            }
        },
        error: function (error) {
            console.error('Lỗi khi hủy lời mời kết bạn:', error);
            toastr.error('Đã xảy ra lỗi khi hủy lời mời kết bạn');
        }
    });
}

// Hàm hủy kết bạn
function unfriend(friendId) {
    $.ajax({
        url: '/Friends/Unfriend',
        type: 'POST',
        data: { friendId: friendId },
        success: function (response) {
            if (response.success) {
                toastr.success('Đã hủy kết bạn');
                // Cập nhật giao diện
                loadFriends();
                // Nếu đang ở tab tìm kiếm, cập nhật kết quả tìm kiếm
                if ($('#user-search').val()) {
                    searchUsers($('#user-search').val());
                }
            } else {
                toastr.error('Không thể hủy kết bạn');
            }
        },
        error: function (error) {
            console.error('Lỗi khi hủy kết bạn:', error);
            toastr.error('Đã xảy ra lỗi khi hủy kết bạn');
        }
    });
}

// Xử lý chấp nhận lời mời từ tab tìm kiếm
$(document).on('click', '.btn-accept-inline', function () {
    const userId = $(this).data('id');

    // Tìm requestId từ userId trong danh sách lời mời đã nhận
    const request = pendingRequests.find(req => req.sender.id === userId);
    console.log(request);
    if (request) {
        acceptFriendRequest(request.id);
    } else {
        // Nếu không tìm thấy trong danh sách hiện tại, tải lại danh sách
        loadPendingRequests();
        toastr.error('Không tìm thấy lời mời kết bạn');
    }
});

// Xử lý từ chối lời mời từ tab tìm kiếm
$(document).on('click', '.btn-decline-inline', function () {
    const userId = $(this).data('id');

    // Tìm requestId từ userId trong danh sách lời mời đã nhận
    const request = pendingRequests.find(req => req.sender.id === userId);
    if (request) {
        declineFriendRequest(request.id);
    } else {
        // Nếu không tìm thấy trong danh sách hiện tại, tải lại danh sách
        loadPendingRequests();
        toastr.error('Không tìm thấy lời mời kết bạn');
    }
});

// Cập nhật badge số lời mời kết bạn
function updateRequestBadge() {
    const count = pendingRequests.length;
    if (count > 0) {
        $('#pending-request-badge').text(count).show();
    } else {
        $('#pending-request-badge').hide();
    }
}

// Hàm định dạng ngày tháng
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// Xử lý lỗi AJAX chung
$(document).ajaxError(function (event, jqXHR, settings, thrownError) {
    if (jqXHR.status === 401) {
        // Người dùng chưa đăng nhập
        window.location.href = '/Account/Login';
    } else if (jqXHR.status === 403) {
        // Người dùng không có quyền
        toastr.error('Bạn không có quyền thực hiện thao tác này');
    }
});
                    