const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .configureLogging(signalR.LogLevel.Information)
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

const messageInput = document.getElementById("message-input");
const sendButton = document.getElementById("send-btn");
const chatContent = document.querySelector(".messages");
const tabContacts = document.getElementById("tab-contacts");
const tabGroups = document.getElementById("tab-groups");
const panelContacts = document.getElementById("panel-contacts");
const panelGroups = document.getElementById("panel-groups");
const groupListElement = document.getElementById("list-group");
const btnNewGroup = document.getElementById("btn-new-group");
const btnGroupMembers = document.getElementById("btn-group-members");
const btnLeaveGroup = document.getElementById("btn-leave-group");
const groupActionsDiv = document.querySelector(".group-actions");
const contactNameElement = document.querySelector(".contact-name");
const btnAddFriend = document.getElementById("add-friend-btn");
const searchUsersInput = document.getElementById("search-users-input");
const searchUsersResults = document.getElementById("search-users-results");

// File upload elements
const fileUploadInput = document.getElementById("file-upload");
const filePreviewContainer = document.getElementById("file-preview-container");

// Modal elements
const modalCreateGroup = document.getElementById("modal-create-group");
const modalGroupMembers = document.getElementById("modal-group-members");
const groupNameInput = document.getElementById("group-name");
const groupDescriptionInput = document.getElementById("group-description");
const btnCreateGroup = document.getElementById("btn-create-group");
const groupMemberList = document.getElementById("group-member-list");
const selectNewMember = document.getElementById("select-new-member");
const btnAddMember = document.getElementById("btn-add-member");
const headerMemberCount = document.getElementById("header-member-count");
const modalSearchUsers = document.getElementById("modal-search-users");

// State variables
let selectedContact = null;
let currentReceiverId = "";
let currentUserEmail = "";
let currentChatType = "contact"; // "contact" or "group"
let currentGroupId = "";
let selectedFiles = []; // Array to store selected files

async function init() {
    await getEmailFromCookie();
    console.log("Initialization complete, currentUserEmail:", currentUserEmail);

    // Initialize SignalR connection
    await startSignalRConnection();

    // Setup event handlers
    setupEventHandlers();

    // Load user's groups
    loadUserGroups();

    // Khởi tạo chức năng theo dõi trạng thái tin nhắn
    if (typeof initMessageStatusTracking === 'function') {
        initMessageStatusTracking();
    }
}

async function getEmailFromCookie() {
    try {
        const res = await fetch('api/user');
        const data = await res.json();
        currentUserEmail = data.email;
        console.log("Current user email:", currentUserEmail);
    } catch (error) {
        console.error("Error fetching user email:", error);
    }
}

async function startSignalRConnection() {
    try {
        await connection.start();
        console.log("Connected to SignalR hub");

        // Setup SignalR event handlers
        setupSignalREvents();
    } catch (err) {
        console.error("SignalR Connection Error:", err);
        setTimeout(startSignalRConnection, 5000); // Try to reconnect after 5 seconds
    }
}

function setupSignalREvents() {
    // For individual messages
    connection.on("ReceiveMessage", (senderEmail, message) => {
        const timestamp = new Date().toLocaleTimeString();
        const receiverEmail = document.querySelector(`.contact[data-user-id="${currentReceiverId}"]`)?.getAttribute("data-email");

        if (currentChatType === "contact" && currentReceiverId) {
            if (senderEmail === currentUserEmail && document.querySelector(`.contact[data-user-id="${currentReceiverId}"]`)) {
                console.log("Displaying own message");
                appendMessage(senderEmail, message, timestamp);
            } else if (senderEmail === receiverEmail) {
                console.log("Displaying message from other");
                appendMessage(senderEmail, message, timestamp);
                markMessageAsRead(messageId, false);
            } else {
                console.log("Showing notification - senderEmail:", senderEmail);
                const senderName = senderEmail.split("@")[0];
                showNotification(senderName, message);
            }
        } else {
            console.log("Not in contact chat or no contact selected, showing notification");
            const senderName = senderEmail.split("@")[0];
            showNotification(senderName, message);
        }
    });

    // Individual message with file
    connection.on("ReceiveMessageWithFile", (senderEmail, message, attachments) => {
        const timestamp = new Date().toLocaleTimeString();
        const receiverEmail = document.querySelector(`.contact[data-user-id="${currentReceiverId}"]`)?.getAttribute("data-email");

        if (currentChatType === "contact" && currentReceiverId) {
            if (senderEmail === currentUserEmail && document.querySelector(`.contact[data-user-id="${currentReceiverId}"]`)) {
                console.log("Displaying own message with file");
                appendMessageWithAttachments(senderEmail, message, timestamp, attachments);
            } else if (senderEmail === receiverEmail) {
                console.log("Displaying message with file from other");
                appendMessageWithAttachments(senderEmail, message, timestamp, attachments);
                markMessageAsRead(messageId, false);
            } else {
                console.log("Showing notification - senderEmail:", senderEmail);
                const senderName = senderEmail.split("@")[0];
                showNotification(senderName, `${message} [Đã gửi file]`);
            }
        } else {
            console.log("Not in contact chat or no contact selected, showing notification");
            const senderName = senderEmail.split("@")[0];
            showNotification(senderName, `${message} [Đã gửi file]`);
        }
    });

    // For group messages
    connection.on("ReceiveGroupMessage", (groupId, senderEmail, message, timestamp) => {
        console.log("Received group message:", groupId, senderEmail, message);

        if (currentChatType === "group" && currentGroupId === groupId) {
            // We're currently viewing this group, append message
            appendMessage(senderEmail, message, timestamp);
            // Nếu không phải tin nhắn của mình, đánh dấu là đã đọc
            if (senderEmail !== currentUserEmai) {
                markMessageAsRead(messageId, true);
            }
        } else {
            // We're not viewing this group, show notification
            const groupName = document.querySelector(`.group-item[data-group-id="${groupId}"]`)?.getAttribute("data-name") || "Group";
            const senderName = senderEmail.split("@")[0];
            showNotification(`${senderName} đã nhắc bạn từ (${groupName})`, message);
        }
    });

    // Group message with file
    connection.on("ReceiveGroupMessageWithFile", (groupId, senderEmail, senderName, message, timestamp, attachments) => {
        console.log("Received group message with file:", groupId, senderEmail, message, attachments);

        if (currentChatType === "group" && currentGroupId === groupId) {
            // We're currently viewing this group, append message with file
            appendMessageWithAttachments(senderEmail, message, timestamp, attachments);
            // Nếu không phải tin nhắn của mình, đánh dấu là đã đọc
            if (senderEmail !== currentUserEmail) {
                markMessageAsRead(messageId, true);
            }
        } else {
            // We're not viewing this group, show notification
            const groupName = document.querySelector(`.group-item[data-group-id="${groupId}"]`)?.getAttribute("data-name") || "Group";
            const displayName = senderName || senderEmail.split("@")[0];
            showNotification(`${displayName} đã nhắc bạn từ (${groupName})`, `${message} [Đã gửi file]`);
        }
    });

    connection.on("MessageStatusUpdated", (messageId, status) => {
        console.log(`Message ${messageId} status updated to ${status}`);
        updateMessageStatusUI(messageId, status);
    });

    connection.on("MessagesRead", (messageIds) => {
        console.log(`Messages read: ${messageIds.join(', ')}`);
        messageIds.forEach(id => {
            updateMessageStatusUI(id, MessageStatusEnum.Read);
        });
    });

    connection.on("GroupMessageStatusUpdated", (messageId, status) => {
        console.log(`Group message ${messageId} status updated to ${status}`);
        updateMessageStatusUI(messageId, status);
    });

    connection.on("GroupMessageRead", (messageId, userId, userEmail) => {
        console.log(`Group message ${messageId} read by ${userEmail}`);
        updateMessageStatusUI(messageId, MessageStatusEnum.Read);
    });

    connection.on("GroupMessagesReadByUser", (groupId, userId, userEmail) => {
        console.log(`All messages in group ${groupId} read by ${userEmail}`);
        if (typeof updateGroupUnreadStatus === 'function') {
            updateGroupUnreadStatus(groupId);
        }
    });
}

function setupEventHandlers() {
    // Tab switching
    tabContacts.addEventListener("click", () => switchTab("contacts"));
    tabGroups.addEventListener("click", () => switchTab("groups"));

    // Messaging
    sendButton.addEventListener("click", sendMessage);
    messageInput.addEventListener("keypress", function (e) {
        if (e.key === "Enter") sendMessage();
    });

    // File upload handling
    fileUploadInput.addEventListener("change", handleFileSelect);

    // Contact selection
    document.querySelectorAll(".contact").forEach(item => {
        item.addEventListener("click", function () {
            if (this.getAttribute("data-is-cloud") === "true") {
                selectCloudContact();
            } else {
                selectContact(this);
            }
        });
    });

    // Group creation
    btnNewGroup.addEventListener("click", () => {
        groupNameInput.value = "";
        groupDescriptionInput.value = "";
        modalCreateGroup.style.display = "block";
    });

    btnAddFriend.addEventListener("click", () => {
        searchUsersInput.value = "";
        searchUsersResults.innerHTML = `
            <div class="empty-search">
                <i class="bi bi-search"></i>
                <p>Nhập tên hoặc email để tìm kiếm</p>
            </div>
        `;
        modalSearchUsers.style.display = "block";
    });

    // Event handler cho ô tìm kiếm người dùng
    searchUsersInput.addEventListener("keyup", function (e) {
        const searchTerm = e.target.value.trim();
        if (searchTerm.length >= 2) {
            searchUsers(searchTerm);
        } else {
            searchUsersResults.innerHTML = `
                <div class="empty-search">
                    <i class="bi bi-search"></i>
                    <p>Nhập tên hoặc email để tìm kiếm</p>
                </div>
            `;
        }
    });

    // Close modals
    document.querySelectorAll(".close").forEach(closeBtn => {
        closeBtn.addEventListener("click", function () {
            modalCreateGroup.style.display = "none";
            modalGroupMembers.style.display = "none";
            modalSearchUsers.style.display = "none";
        });
    });

    // Create group button
    btnCreateGroup.addEventListener("click", createNewGroup);

    // Group actions
    btnGroupMembers.addEventListener("click", openGroupMembersModal);
    btnLeaveGroup.addEventListener("click", leaveCurrentGroup);
    btnAddMember.addEventListener("click", addMemberToGroup);

    // Close modals when clicking outside
    window.addEventListener("click", function (event) {
        if (event.target === modalCreateGroup) {
            modalCreateGroup.style.display = "none";
        }
        if (event.target === modalGroupMembers) {
            modalGroupMembers.style.display = "none";
        }
        if (event.target === modalSearchUsers) {
            modalSearchUsers.style.display = "none";
        }
    });

    // Track changes to current chat type
    const originalSendButton = sendButton.innerHTML;

    // Cập nhật nút gửi khi chuyển đổi giữa các chế độ chat
    function updateSendButtonState() {
        if (currentChatType === 'cloud') {
            sendButton.innerHTML = '<i class="bi bi-cloud-upload"></i>';
            sendButton.setAttribute('title', 'Lưu vào Cloud');
        } else {
            sendButton.innerHTML = originalSendButton;
            sendButton.setAttribute('title', 'Gửi tin nhắn');
        }
    }

    // Lắng nghe sự kiện khi tab contacts được click
    tabContacts.addEventListener("click", function () {
        setTimeout(updateSendButtonState, 100); // Đợi DOM cập nhật xong
    });

    // Lắng nghe sự kiện khi tab groups được click
    tabGroups.addEventListener("click", function () {
        setTimeout(updateSendButtonState, 100); // Đợi DOM cập nhật xong
    });

    // Lắng nghe sự kiện khi một liên hệ được click
    document.querySelectorAll(".contact, .group-item").forEach(item => {
        item.addEventListener("click", function () {
            setTimeout(updateSendButtonState, 100); // Đợi DOM cập nhật xong
        });
    });

    // Cập nhật trạng thái ban đầu
    updateSendButtonState();
}

// File selection handling
function handleFileSelect(event) {
    selectedFiles = Array.from(event.target.files);

    // Clear preview container
    filePreviewContainer.innerHTML = '';

    // Create preview for each file
    selectedFiles.forEach((file, index) => {
        const filePreview = document.createElement('div');
        filePreview.className = 'file-preview';

        // Show file icon based on type
        let fileIcon = '<i class="fas fa-file"></i>';
        if (file.type.startsWith('image/')) {
            fileIcon = '<i class="fas fa-file-image"></i>';
        } else if (file.type.startsWith('video/')) {
            fileIcon = '<i class="fas fa-file-video"></i>';
        } else if (file.type.startsWith('audio/')) {
            fileIcon = '<i class="fas fa-file-audio"></i>';
        } else if (file.type === 'application/pdf') {
            fileIcon = '<i class="fas fa-file-pdf"></i>';
        }

        // Create remove button
        const removeButton = document.createElement('span');
        removeButton.className = 'remove-file';
        removeButton.innerHTML = '&times;';
        removeButton.onclick = function () {
            removeSelectedFile(index);
        };

        // Prepare file info
        filePreview.innerHTML = `
            ${fileIcon}
            <span class="file-name">${file.name}</span>
            <span class="file-size">${formatFileSize(file.size)}</span>
        `;
        filePreview.appendChild(removeButton);

        filePreviewContainer.appendChild(filePreview);
    });

    if (selectedFiles.length > 0) {
        filePreviewContainer.style.display = 'flex';
    } else {
        filePreviewContainer.style.display = 'none';
    }
}

// Remove a file from the selected files array
function removeSelectedFile(index) {
    selectedFiles.splice(index, 1);
    handleFileSelect({ target: { files: selectedFiles } });

    // If we removed all files, reset the file input
    if (selectedFiles.length === 0) {
        fileUploadInput.value = '';
    }
}

// Format file size for display
function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    else if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    else return (bytes / 1048576).toFixed(1) + ' MB';
}

function switchTab(tab) {
    // Clear file selection when switching tabs
    clearFileSelection();

    if (tab === "contacts") {
        tabContacts.classList.add("active");
        tabGroups.classList.remove("active");
        panelContacts.style.display = "block";
        panelGroups.style.display = "none";
    } else {
        tabGroups.classList.add("active");
        tabContacts.classList.remove("active");
        panelGroups.style.display = "block";
        panelContacts.style.display = "none";
    }

    // Reset chat area
    currentChatType = "";
    currentGroupId = "";
    currentReceiverId = "";
    contactNameElement.textContent = "Chọn một liên hệ hoặc nhóm";
    headerMemberCount.textContent = "";
    chatContent.innerHTML = "";
    groupActionsDiv.style.display = "none";
}

function selectContact(contactElement) {
    // Reset all active states
    document.querySelectorAll(".contact, .group-item").forEach(c => c.classList.remove("active"));

    // Clear file selection
    clearFileSelection();

    // Set this contact as active
    contactElement.classList.add("active");

    // Update state
    selectedContact = contactElement.getAttribute("data-is-cloud") === "true" ? "Cloud của tôi@gmail.com" : contactElement.getAttribute("data-email");
    currentReceiverId = contactElement.getAttribute("data-user-id");
    currentChatType = "contact";
    currentGroupId = "";

    // Update UI
    contactNameElement.textContent = selectedContact.split("@")[0];
    headerMemberCount.textContent = "";
    groupActionsDiv.style.display = "none";

    // Clear chat and load messages
    chatContent.innerHTML = "";
    loadMessages(currentReceiverId);

    const contactEmail = contactElement.getAttribute("data-email");
    if (contactEmail) {
        markAllMessagesAsRead(contactEmail);
    }
}

function selectGroup(groupElement) {
    // Reset all active states
    document.querySelectorAll(".contact, .group-item").forEach(c => c.classList.remove("active"));

    // Clear file selection
    clearFileSelection();

    // Set this group as active
    groupElement.classList.add("active");

    // Update state
    currentChatType = "group";
    currentGroupId = groupElement.getAttribute("data-group-id");
    currentReceiverId = "";
    selectedContact = null;

    // Update UI
    contactNameElement.textContent = groupElement.getAttribute("data-name");
    groupActionsDiv.style.display = "flex";

    // Update member count in header
    updateHeaderMemberCount(currentGroupId);

    // Clear chat and load group messages
    chatContent.innerHTML = "";
    loadGroupMessages(currentGroupId);

    // Join the SignalR group
    connection.invoke("JoinGroup", currentGroupId)
        .catch(err => console.error("Error joining group:", err));

    // Đánh dấu tất cả tin nhắn nhóm là đã đọc
    const groupId = groupElement.getAttribute("data-group-id");
    if (groupId) {
        markAllMessagesAsRead(null, groupId);
    }
}

function clearFileSelection() {
    fileUploadInput.value = '';
    selectedFiles = [];
    filePreviewContainer.innerHTML = '';
    filePreviewContainer.style.display = 'none';
}

async function loadUserGroups() {
    try {
        const response = await fetch('/GroupChat/GetUserGroups');
        const groups = await response.json();

        // Clear existing groups
        groupListElement.innerHTML = "";

        // Add each group to the list
        groups.forEach(group => {
            const groupElement = document.createElement("div");
            groupElement.classList.add("group-item");
            groupElement.setAttribute("data-group-id", group.id);
            groupElement.setAttribute("data-name", group.name);

            groupElement.innerHTML = `
                <span class="group-name">${group.name}</span>
            `;

            // Add click event
            groupElement.addEventListener("click", function () {
                selectGroup(this);
            });

            groupListElement.appendChild(groupElement);
        });
    } catch (error) {
        console.error("Error loading groups:", error);
    }
}

async function updateHeaderMemberCount(groupId) {
    if (!groupId) {
        headerMemberCount.textContent = "";
        return;
    }

    try {
        const response = await fetch(`/GroupChat/GetMembers?groupId=${groupId}`);
        const members = await response.json();

        headerMemberCount.textContent = `${members.length} thành viên`;
    } catch (error) {
        console.error("Error updating member count:", error);
        headerMemberCount.textContent = "";
    }
}

async function createNewGroup() {
    const name = groupNameInput.value.trim();
    const description = groupDescriptionInput.value.trim();

    if (!name) {
        alert("Tên nhóm không được để trống");
        return;
    }

    try {
        const formData = new FormData();
        formData.append('name', name);
        formData.append('description', description);

        const response = await fetch('/GroupChat/CreateGroup', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            // Close the modal
            modalCreateGroup.style.display = "none";

            // Refresh groups list
            loadUserGroups();

            // Switch to groups tab
            switchTab("groups");
        } else {
            alert("Không thể tạo nhóm: " + result.message);
        }
    } catch (error) {
        console.error("Error creating group:", error);
        alert("Đã xảy ra lỗi khi tạo nhóm");
    }
}

async function loadGroupMessages(groupId) {
    try {
        const response = await fetch(`/GroupChat/GetGroupMessages?groupId=${groupId}`);
        const messages = await response.json();

        // Clear existing messages
        chatContent.innerHTML = "";

        // Add each message
        messages.forEach(msg => {
            if (msg.hasAttachment && msg.attachments && msg.attachments.length > 0) {
                appendMessageWithAttachments(msg.senderEmail, msg.content, msg.timestamp, msg.attachments, msg.id);
            } else {
                appendMessage(msg.senderEmail, msg.content, msg.timestamp, msg.id);
            }
        });

        document.querySelectorAll('.message.sent').forEach(messageElement => {
            addMessageStatus(messageElement);
        });

        // Scroll to bottom
        chatContent.scrollTop = chatContent.scrollHeight;
    } catch (error) {
        console.error("Error loading group messages:", error);
    }
}

async function loadMessages(receiverId) {
    try {
        const response = await fetch(`/Chat/GetMessages?receiverId=${receiverId}`);
        const messages = await response.json();
        // Clear existing messages
        chatContent.innerHTML = "";

        // Add each message
        messages.forEach(msg => {
            if (msg.hasAttachment && msg.attachments && msg.attachments.length > 0) {
                appendMessageWithAttachments(msg.senderEmail, msg.content, msg.timestamp, msg.attachments, msg.id);
            } else {
                appendMessage(msg.senderEmail, msg.content, msg.timestamp, msg.id);
            }
        });
        document.querySelectorAll('.message.sent').forEach(messageElement => {
            addMessageStatus(messageElement);
        });

        // Scroll to bottom
        chatContent.scrollTop = chatContent.scrollHeight;
    } catch (error) {
        console.error("Error loading messages:", error);
    }
}

async function sendMessage() {
    const content = messageInput.value.trim();
    if (!content && selectedFiles.length === 0) return;

    try {
        if (currentChatType === "cloud") {
            await uploadToCloud();
        } else if (currentChatType === "contact" && currentReceiverId) {
            if (selectedFiles.length > 0) {
                await sendMessageWithFile(currentReceiverId, content);
            } else {
                // Send direct message
                const formData = new FormData();
                formData.append('receiverId', currentReceiverId);
                formData.append('content', content);

                await fetch("/Chat/SendMessage", {
                    method: "POST",
                    body: formData
                });
            }
        } else if (currentChatType === "group" && currentGroupId) {
            if (selectedFiles.length > 0) {
                // Send group message with file
                await sendGroupMessageWithFile(currentGroupId, content);
            } else {
                // Send group message
                const formData = new FormData();
                formData.append('groupId', currentGroupId);
                formData.append('content', content);

                await fetch("/GroupChat/SendMessage", {
                    method: "POST",
                    body: formData
                });
            }
        } else {
            console.log("No recipient selected");
            return;
        }

        // Clear input field and file selection
        messageInput.value = "";
        clearFileSelection();
    } catch (error) {
        console.error("Error sending message:", error);
    }
}

async function sendMessageWithFile(receiverId, content) {
    try {
        const formData = new FormData();
        formData.append('receiverId', receiverId);
        formData.append('content', content || '');

        if (selectedFiles.length === 1) {
            // Single file upload
            formData.append('file', selectedFiles[0]);

            await fetch("/Chat/SendMessageWithFile", {
                method: "POST",
                body: formData
            });
        } else {
            // Multiple files upload
            selectedFiles.forEach(file => {
                formData.append('files', file);
            });

            await fetch("/Chat/SendMessageWithMultipleFiles", {
                method: "POST",
                body: formData
            });
        }
    } catch (error) {
        console.error("Error sending file message:", error);
        alert("Failed to send file message. Please try again.");
    }
}

async function sendGroupMessageWithFile(groupId, content) {
    try {
        const formData = new FormData();
        formData.append('groupId', groupId);
        formData.append('content', content || '');

        if (selectedFiles.length === 1) {
            // Single file upload
            formData.append('file', selectedFiles[0]);

            await fetch("/GroupChat/SendMessageWithFile", {
                method: "POST",
                body: formData
            });
        } else {
            // Multiple files upload
            selectedFiles.forEach(file => {
                formData.append('files', file);
            });

            await fetch("/GroupChat/SendMessageWithMultipleFiles", {
                method: "POST",
                body: formData
            });
        }
    } catch (error) {
        console.error("Error sending group file message:", error);
        alert("Failed to send file message to group. Please try again.");
    }
}

async function openGroupMembersModal() {
    if (!currentGroupId) return;

    try {
        // Load members
        const response = await fetch(`/GroupChat/GetMembers?groupId=${currentGroupId}`);
        const members = await response.json();

        // Clear existing list
        groupMemberList.innerHTML = "";

        // Add each member
        members.forEach(member => {
            const memberElement = document.createElement("div");
            memberElement.classList.add("member-item");
            memberElement.innerHTML = `
                <div class="member-info">
                    <span class="member-name">${member.userName}</span>
                    <span class="member-email">${member.email}</span>
                </div>
                ${member.isAdmin ? '<span class="member-admin">Admin</span>' : ''}
            `;
            groupMemberList.appendChild(memberElement);
        });

        // Update member count in header
        headerMemberCount.textContent = `${members.length} thành viên`;

        // Load potential members for adding
        const nonMemberResponse = await fetch(`/GroupChat/GetNonMembers?groupId=${currentGroupId}`);
        const nonMembers = await nonMemberResponse.json();

        // Clear and populate select dropdown
        selectNewMember.innerHTML = '<option value="">Chọn người dùng</option>';

        nonMembers.forEach(user => {
            const option = document.createElement("option");
            option.value = user.id;
            option.textContent = `${user.userName} (${user.email})`;
            selectNewMember.appendChild(option);
        });

        // Show modal
        modalGroupMembers.style.display = "block";
    } catch (error) {
        console.error("Error loading group members:", error);
    }
}

async function addMemberToGroup() {
    const userId = selectNewMember.value;
    if (!userId || !currentGroupId) return;

    try {
        const formData = new FormData();
        formData.append('groupId', currentGroupId);
        formData.append('userId', userId);

        const response = await fetch('/GroupChat/AddMember', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            // Refresh the members list
            openGroupMembersModal();
            // Update member Count
            updateHeaderMemberCount(currentGroupId);
        } else {
            alert("Không thể thêm thành viên: " + result.message);
        }
    } catch (error) {
        console.error("Error adding member:", error);
    }
}

async function leaveCurrentGroup() {
    if (!currentGroupId) return;

    if (!confirm("Bạn có chắc chắn muốn rời khỏi nhóm này?")) return;

    try {
        const formData = new FormData();
        formData.append('groupId', currentGroupId);

        const response = await fetch('/GroupChat/LeaveGroup', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            await connection.invoke("LeaveGroup", currentGroupId);
            // Switch back to groups tab and reload groups
            switchTab("groups");

            loadUserGroups();
        } else {
            alert("Không thể rời khỏi nhóm: " + result.message);
        }
    } catch (error) {
        console.error("Error leaving group:", error);
    }
}

function appendMessage(senderEmail, message, timestamp, messageId) {
    const div = document.createElement("div");
    const isSentByMe = senderEmail === currentUserEmail;

    div.classList.add("message");
    if (isSentByMe) {
        div.classList.add("sent");
    }

    let localTimestamp = timestamp;
    if (typeof timestamp === "number") {
        localTimestamp = new Date(timestamp).toLocaleTimeString();
    }

    let messageContent = `
        <div class="message-content">
            <p>${message}</p>
            <span class="timestamp">${localTimestamp}</span>
        </div>
    `;

    const senderName = senderEmail.split("@")[0];
    if (!isSentByMe) {
        div.innerHTML = `
            <div class="sender-info">
                <img class="avatar" src="/img/user_ava.svg" alt="Avatar">
                <span class="sender-name">${senderName}</span>
            </div>
            ${messageContent}
        `;
    } else {
        div.innerHTML = messageContent;
    }
    chatContent.appendChild(div);
    chatContent.scrollTop = chatContent.scrollHeight;
}

function simulateMessageStatusUpdates(messageElement) {
    if (!messageElement) return;

    const messageId = messageElement.dataset.messageId;
    if (!messageId) return;

    // Sau 2 giây, cập nhật trạng thái thành "Đã nhận"
    setTimeout(() => {
        updateMessageStatusUI(messageId, 1);

        // Sau thêm 3 giây nữa, cập nhật thành "Đã đọc"
        setTimeout(() => {
            updateMessageStatusUI(messageId, 2);
        }, 3000);
    }, 2000);
}

// Hàm thêm trạng thái tin nhắn vào một phần tử tin nhắn đã hiển thị
function addMessageStatus(messageElement, status = 0) {
    if (!messageElement) return;

    // Chỉ thêm trạng thái cho tin nhắn của người dùng hiện tại
    if (!messageElement.classList.contains('sent')) return;

    // Tạo hoặc tìm ID cho tin nhắn
    let messageId = messageElement.dataset.messageId;
    if (!messageId) {
        messageId = 'msg_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
        messageElement.dataset.messageId = messageId;
    }

    // Kiểm tra xem đã có container status chưa
    let statusContainer = messageElement.querySelector('.message-status-container');

    if (!statusContainer) {
        statusContainer = document.createElement('div');
        statusContainer.className = 'message-status-container';
        messageElement.appendChild(statusContainer);
    }

    // Tạo HTML cho trạng thái tin nhắn
    const statusTitle = status === 0 ? 'Đã gửi' : (status === 1 ? 'Đã nhận' : 'Đã đọc');
    const statusIcon = StatusIconsMap[status] || StatusIconsMap[0];

    statusContainer.innerHTML = `
        <span class="message-status" 
            data-message-id="${messageId}" 
            data-status="${status}" 
            title="${statusTitle}">
            ${statusIcon}
        </span>
    `;

    return messageId;
}

function appendMessageWithAttachments(senderEmail, message, timestamp, attachments) {
    const div = document.createElement("div");
    const isSentByMe = senderEmail === currentUserEmail;

    div.classList.add("message");
    if (isSentByMe) {
        div.classList.add("sent");
    }

    let localTimestamp = timestamp;
    if (typeof timestamp === "number") {
        localTimestamp = new Date(timestamp).toLocaleTimeString();
    }

    const senderName = senderEmail.split("@")[0];

    // Create attachment HTML
    let attachmentsHtml = '';
    if (attachments && attachments.length > 0) {
        attachmentsHtml = '<div class="message-attachments">';
        attachments.forEach(attachment => {
            const fileExtension = attachment.fileName.split('.').pop().toLowerCase();
            let attachmentHtml = '';

            if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(fileExtension)) {
                // Image preview
                attachmentHtml = `
                    <div class="attachment-item image">
                        <a href="/Chat/DownloadFile?fileId=${attachment.id}" target="_blank">
                            <img src="/Chat/DownloadFile?fileId=${attachment.id}" alt="${attachment.fileName}">
                        </a>
                        <div class="attachment-info">
                            <span class="attachment-name">${attachment.fileName}</span>
                            <span class="attachment-size">${formatFileSize(attachment.fileSize)}</span>
                        </div>
                    </div>
                `;
            } else {
                // Generic file
                let icon = 'fas fa-file';
                if (['pdf'].includes(fileExtension)) icon = 'fas fa-file-pdf';
                else if (['doc', 'docx'].includes(fileExtension)) icon = 'fas fa-file-word';
                else if (['xls', 'xlsx'].includes(fileExtension)) icon = 'fas fa-file-excel';
                else if (['ppt', 'pptx'].includes(fileExtension)) icon = 'fas fa-file-powerpoint';
                else if (['zip', 'rar', '7z'].includes(fileExtension)) icon = 'fas fa-file-archive';
                else if (['mp3', 'wav', 'ogg'].includes(fileExtension)) icon = 'fas fa-file-audio';
                else if (['mp4', 'avi', 'mov', 'wmv'].includes(fileExtension)) icon = 'fas fa-file-video';

                attachmentHtml = `
                    <div class="attachment-item file">
                        <a href="/Chat/DownloadFile?fileId=${attachment.id}" target="_blank">
                            <i class="${icon}"></i>
                            <div class="attachment-info">
                                <span class="attachment-name">${attachment.fileName}</span>
                                <span class="attachment-size">${formatFileSize(attachment.fileSize)}</span>
                            </div>
                        </a>
                    </div>
                `;
            }

            attachmentsHtml += attachmentHtml;
        });
        attachmentsHtml += '</div>';
    }

    let messageContent = `
        <div class="message-content">
            <p>${message}</p>
            ${attachmentsHtml}
            <span class="timestamp">${localTimestamp}</span>
        </div>
    `;

    if (!isSentByMe) {
        div.innerHTML = `
            <div class="sender-info">
                <img class="avatar" src="/img/user_ava.svg" alt="Avatar">
                <span class="sender-name">${senderName}</span>
            </div>
            ${messageContent}
        `;
    } else {
        div.innerHTML = messageContent;
    }
    chatContent.appendChild(div);
    chatContent.scrollTop = chatContent.scrollHeight;
}

function showNotification(senderEmail, message) {
    const notification = document.createElement("div");
    notification.classList.add("custom-toast");
    notification.innerHTML = `
        <strong>${senderEmail}</strong>: ${message}
        <div class="toast-progress-bar"></div>
    `;
    document.body.appendChild(notification);
    setTimeout(() => notification.remove(), 5000);
}

// Hàm tìm kiếm người dùng
function searchUsers(keyword) {
    searchUsersResults.innerHTML = `
        <div class="loading-spinner">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Đang tìm kiếm...</span>
            </div>
        </div>
    `;

    $.ajax({
        url: '/Users/SearchUsers',
        type: 'GET',
        data: { keyword: keyword },
        success: function (response) {
            renderUserSearchResults(response);
        },
        error: function (error) {
            console.error('Lỗi khi tìm kiếm người dùng:', error);
            searchUsersResults.innerHTML = `
                <div class="alert alert-danger">
                    Có lỗi xảy ra khi tìm kiếm người dùng.
                </div>
            `;
        }
    });
}

// Hàm hiển thị kết quả tìm kiếm người dùng
function renderUserSearchResults(users) {
    if (!users || users.length === 0) {
        searchUsersResults.innerHTML = `
            <div class="search-no-results">
                <p>Không tìm thấy người dùng nào khớp với từ khóa.</p>
            </div>
        `;
        return;
    }

    let html = '';
    users.forEach(user => {
        const firstLetter = user.name ? user.name.charAt(0) : user.username.charAt(0);
        const displayName = user.name || user.username;

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

    searchUsersResults.innerHTML = html;

    // Thêm event listeners cho các nút hành động
    addFriendActionListeners();
}

// Hàm thêm các event handlers cho các nút trong kết quả tìm kiếm
function addFriendActionListeners() {
    // Xử lý sự kiện nút kết bạn
    document.querySelectorAll('.btn-add').forEach(btn => {
        btn.addEventListener('click', function () {
            const userId = this.getAttribute('data-id');
            sendFriendRequest(userId);
        });
    });

    // Xử lý sự kiện nút chấp nhận lời mời
    document.querySelectorAll('.btn-accept-inline').forEach(btn => {
        btn.addEventListener('click', function () {
            const userId = this.getAttribute('data-id');
            acceptFriendRequest(userId);
        });
    });

    // Xử lý sự kiện nút từ chối lời mời
    document.querySelectorAll('.btn-decline-inline').forEach(btn => {
        btn.addEventListener('click', function () {
            const userId = this.getAttribute('data-id');
            declineFriendRequest(userId);
        });
    });

    // Xử lý sự kiện nút chat
    document.querySelectorAll('.btn-chat').forEach(btn => {
        btn.addEventListener('click', function () {
            const userId = this.getAttribute('data-id');
            // Tìm kiếm liên hệ tương ứng và chọn để chat
            const contact = document.querySelector(`.contact[data-user-id="${userId}"]`);
            if (contact) {
                selectContact(contact);
                modalSearchUsers.style.display = "none";
                // Chuyển sang tab liên hệ nếu đang ở tab nhóm
                if (tabGroups.classList.contains("active")) {
                    switchTab("contacts");
                }
            }
        });
    });

    // Xử lý sự kiện nút hủy kết bạn
    document.querySelectorAll('.btn-unfriend').forEach(btn => {
        btn.addEventListener('click', function () {
            const userId = this.getAttribute('data-id');
            const userName = this.getAttribute('data-name');
            if (confirm(`Bạn có chắc muốn hủy kết bạn với ${userName}?`)) {
                unfriend(userId);
            }
        });
    });
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
                const keyword = searchUsersInput.value.trim();
                searchUsers(keyword);
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
function acceptFriendRequest(userId) {
    $.ajax({
        url: '/Friends/GetPendingRequests',
        type: 'GET',
        success: function (requests) {
            // Tìm request ID từ user ID
            const request = requests.find(req => req.sender.id === userId);
            if (request) {
                $.ajax({
                    url: '/Friends/AcceptFriendRequest',
                    type: 'POST',
                    data: { requestId: request.id },
                    success: function (response) {
                        if (response.success) {
                            toastr.success('Đã chấp nhận lời mời kết bạn');
                            // Cập nhật giao diện
                            const keyword = searchUsersInput.value.trim();
                            searchUsers(keyword);
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
        },
        error: function (error) {
            console.error('Lỗi khi lấy danh sách lời mời:', error);
            toastr.error('Đã xảy ra lỗi khi xử lý lời mời kết bạn');
        }
    });
}

// Hàm từ chối lời mời kết bạn
function declineFriendRequest(userId) {
    $.ajax({
        url: '/Friends/GetPendingRequests',
        type: 'GET',
        success: function (requests) {
            // Tìm request ID từ user ID
            const request = requests.find(req => req.sender.id === userId);
            if (request) {
                $.ajax({
                    url: '/Friends/DeclineFriendRequest',
                    type: 'POST',
                    data: { requestId: request.id },
                    success: function (response) {
                        if (response.success) {
                            toastr.success('Đã từ chối lời mời kết bạn');
                            // Cập nhật giao diện
                            const keyword = searchUsersInput.value.trim();
                            searchUsers(keyword);
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
        },
        error: function (error) {
            console.error('Lỗi khi lấy danh sách lời mời:', error);
            toastr.error('Đã xảy ra lỗi khi xử lý lời mời kết bạn');
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
                const keyword = searchUsersInput.value.trim();
                searchUsers(keyword);
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

const MessageStatusEnum = {
    Sent: 0,
    Delivered: 1,
    Read: 2
};

// Định nghĩa icon cho các trạng thái
const StatusIconsMap = {
    0: '<i class="fas fa-check text-secondary"></i>',       // Đã gửi
    1: '<i class="fas fa-check-double text-secondary"></i>', // Đã nhận
    2: '<i class="fas fa-check-double text-primary"></i>'    // Đã đọc
};

// Cập nhật trạng thái tin nhắn
function updateMessageStatusUI(messageId, status) {
    if (!messageId) return;

    const statusElement = document.querySelector(`.message-status[data-message-id="${messageId}"]`);
    if (statusElement) {
        status = parseInt(status || 0);
        const statusTitle = status === 0 ? 'Đã gửi' : (status === 1 ? 'Đã nhận' : 'Đã đọc');
        const statusIcon = StatusIconsMap[status] || StatusIconsMap[0];

        // Thêm class để kích hoạt hiệu ứng
        statusElement.classList.add('status-updating');

        // Cập nhật nội dung và thuộc tính
        statusElement.innerHTML = statusIcon;
        statusElement.setAttribute('data-status', status);
        statusElement.setAttribute('title', statusTitle);

        // Xóa class hiệu ứng sau khi hoàn thành
        setTimeout(() => {
            statusElement.classList.remove('status-updating');
        }, 300);
    }
}

// Đánh dấu tin nhắn là đã đọc
function markMessageAsRead(messageId, isGroupMessage = false) {
    if (!messageId) return;

    if (isGroupMessage) {
        connection.invoke("MarkGroupMessageAsRead", messageId)
            .catch(err => console.error(`Error marking group message as read: ${err.toString()}`));
    } else {
        connection.invoke("MarkMessageAsRead", messageId)
            .catch(err => console.error(`Error marking message as read: ${err.toString()}`));
    }
}

// Đánh dấu tất cả tin nhắn là đã đọc
function markAllMessagesAsRead(contactEmail = null, groupId = null) {
    if (contactEmail) {
        connection.invoke("MarkAllMessagesAsRead", contactEmail)
            .catch(err => console.error(`Error marking messages as read: ${err.toString()}`));
    } else if (groupId) {
        connection.invoke("MarkAllGroupMessagesAsRead", groupId)
            .catch(err => console.error(`Error marking group messages as read: ${err.toString()}`));
    }
}

// Hàm chọn liên hệ cloud
function selectCloudContact() {
    // Reset all active states
    document.querySelectorAll(".contact, .group-item").forEach(c => c.classList.remove("active"));

    // Clear file selection
    clearFileSelection();

    // Get the cloud contact element
    const cloudContact = document.querySelector('.contact[data-is-cloud="true"]');
    if (cloudContact) {
        // Set this contact as active
        cloudContact.classList.add("active");

        // Update state
        selectedContact = "Cloud của tôi@gmail.com";
        currentReceiverId = cloudContact.getAttribute("data-user-id");
        currentChatType = "cloud";
        currentGroupId = "";

        // Update UI
        contactNameElement.textContent = "Cloud của tôi";
        headerMemberCount.textContent = "";
        groupActionsDiv.style.display = "none";

        // Cập nhật placeholder và icon cho nút gửi
        messageInput.placeholder = 'Nhập nội dung hoặc đính kèm file để lưu vào Cloud...';
        sendButton.innerHTML = '<i class="bi bi-cloud-upload"></i>';
        sendButton.setAttribute('title', 'Lưu vào Cloud');

        // Clear chat and load cloud files
        chatContent.innerHTML = "";
        loadCloudFiles();
    }
}

// Hàm tải dữ liệu lưu trữ cloud
async function loadCloudFiles() {
    try {
        const response = await fetch('/Chat/GetCloudStorageData');
        const data = await response.json();

        if (data.success) {
            // Clear existing messages
            chatContent.innerHTML = "";

            // Hiển thị thông tin dung lượng
            const storageInfo = data.storageInfo;
            const storageInfoDiv = document.createElement('div');
            storageInfoDiv.className = 'storage-info-container';
            storageInfoDiv.innerHTML = `
                <div class="storage-info">
                    <div class="storage-progress">
                        <div class="storage-progress-bar" style="width: ${storageInfo.percentage}%"></div>
                    </div>
                    <div class="storage-details">
                        <span>${storageInfo.usedFormatted} / ${storageInfo.limitFormatted}</span>
                        <span>${storageInfo.percentage.toFixed(1)}% đã sử dụng</span>
                    </div>
                </div>
            `;
            chatContent.appendChild(storageInfoDiv);

            // Hiển thị danh sách tin nhắn và file
            // Tin nhắn đã được sắp xếp từ sớm đến muộn bởi API
            data.messages.forEach(msg => {
                appendCloudMessage(msg);
            });

            // Scroll to bottom để hiển thị tin nhắn mới nhất
            chatContent.scrollTop = chatContent.scrollHeight;
        } else {
            chatContent.innerHTML = `
                <div class="error-message">
                    <p>Không thể tải dữ liệu Cloud. ${data.message}</p>
                </div>
            `;
        }
    } catch (error) {
        console.error("Error loading cloud files:", error);
        chatContent.innerHTML = `
            <div class="error-message">
                <p>Đã xảy ra lỗi khi tải dữ liệu Cloud.</p>
            </div>
        `;
    }
}

// Hàm hiển thị tin nhắn cloud
function appendCloudMessage(message) {
    const div = document.createElement("div");
    div.classList.add("message", "cloud-message");
    div.setAttribute("data-message-id", message.id);

    const timestamp = new Date(message.createdAt).toLocaleTimeString();

    // Tạo HTML cho các tệp đính kèm
    let attachmentsHtml = '';
    if (message.attachments && message.attachments.length > 0) {
        attachmentsHtml = '<div class="message-attachments">';
        message.attachments.forEach(attachment => {
            const fileExtension = attachment.fileName.split('.').pop().toLowerCase();
            let attachmentHtml = '';

            // Sử dụng storagePath trực tiếp từ dữ liệu attachment
            const cloudinaryUrl = attachment.storagePath;

            if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(fileExtension)) {
                // Image preview với URL Cloudinary
                attachmentHtml = `
                    <div class="attachment-item image">
                        <a href="${cloudinaryUrl}" target="_blank">
                            <img src="${cloudinaryUrl}" alt="${attachment.fileName}">
                        </a>
                        <div class="attachment-info">
                            <span class="attachment-name">${attachment.fileName}</span>
                            <span class="attachment-size">${formatFileSize(attachment.fileSize)}</span>
                        </div>
                    </div>
                `;
            } else {
                // Generic file
                let icon = 'fas fa-file';
                if (['pdf'].includes(fileExtension)) icon = 'fas fa-file-pdf';
                else if (['doc', 'docx'].includes(fileExtension)) icon = 'fas fa-file-word';
                else if (['xls', 'xlsx'].includes(fileExtension)) icon = 'fas fa-file-excel';
                else if (['ppt', 'pptx'].includes(fileExtension)) icon = 'fas fa-file-powerpoint';
                else if (['zip', 'rar', '7z'].includes(fileExtension)) icon = 'fas fa-file-archive';
                else if (['mp3', 'wav', 'ogg'].includes(fileExtension)) icon = 'fas fa-file-audio';
                else if (['mp4', 'avi', 'mov', 'wmv'].includes(fileExtension)) icon = 'fas fa-file-video';

                attachmentHtml = `
                    <div class="attachment-item file">
                        <a href="${cloudinaryUrl}" target="_blank">
                            <i class="${icon}"></i>
                            <div class="attachment-info">
                                <span class="attachment-name">${attachment.fileName}</span>
                                <span class="attachment-size">${formatFileSize(attachment.fileSize)}</span>
                            </div>
                        </a>
                    </div>
                `;
            }

            attachmentsHtml += attachmentHtml;
        });
        attachmentsHtml += '</div>';
    }

    div.innerHTML = `
        <div class="cloud-message-header">
            <span class="timestamp">${timestamp}</span>
            <button class="btn-delete-cloud" data-message-id="${message.id}" title="Xóa">
                <i class="bi bi-trash"></i>
            </button>
        </div>
        <div class="message-content">
            ${message.content ? `<p>${message.content}</p>` : ''}
            ${attachmentsHtml}
        </div>
    `;

    // Add event listener to delete button
    div.querySelector('.btn-delete-cloud').addEventListener('click', function () {
        const messageId = this.getAttribute('data-message-id');
        if (confirm('Bạn có chắc chắn muốn xóa nội dung này không?')) {
            deleteCloudMessage(messageId);
        }
    });

    chatContent.appendChild(div);
}

// Hàm xóa tin nhắn cloud
async function deleteCloudMessage(messageId) {
    try {
        const response = await fetch('/Chat/DeleteCloudMessage', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `messageId=${messageId}`
        });

        const result = await response.json();

        if (result.success) {
            // Remove message from UI
            const messageElement = document.querySelector(`.cloud-message[data-message-id="${messageId}"]`);
            if (messageElement) {
                messageElement.remove();
            }

            // Reload cloud files to update storage info
            loadCloudFiles();

            toastr.success('Nội dung đã được xóa thành công!');
        } else {
            toastr.error(result.message || 'Không thể xóa nội dung.');
        }
    } catch (error) {
        console.error('Error deleting cloud message:', error);
        toastr.error('Đã xảy ra lỗi khi xóa nội dung.');
    }
}

// Hàm gửi file lên cloud
async function uploadToCloud() {
    const content = messageInput.value.trim();
    try {
        // Xác định endpoint dựa vào có file đính kèm hay không
        let endpoint = '/Chat/UploadTextToCloud';
        const formData = new FormData();
        formData.append('content', content);

        if (selectedFiles.length > 0) {
            // Nếu có file đính kèm, sử dụng endpoint cho file
            endpoint = '/Chat/UploadToCloud';

            // Thêm files vào formData
            selectedFiles.forEach(file => {
                formData.append('files', file);
            });

            // Kiểm tra nếu không có nội dung và không có file
            if (!content && selectedFiles.length === 0) {
                toastr.error('Vui lòng nhập nội dung hoặc chọn file để lưu vào Cloud.');
                return;
            }

            toastr.info('Đang tải file lên Cloud...');
        } else {
            // Đây là trường hợp chỉ có text
            if (!content) {
                toastr.error('Vui lòng nhập nội dung để lưu vào Cloud.');
                return;
            }
            toastr.info('Đang lưu ghi chú vào Cloud...');
        }

        const response = await fetch(endpoint, {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        if (result.success) {
            // Clear input và file selection
            messageInput.value = "";
            clearFileSelection();

            // Thông báo thành công
            toastr.success('Đã lưu nội dung vào Cloud thành công!');

            // Reload cloud files và đảm bảo cuộn xuống dưới sau khi tải lại
            await loadCloudFiles();
            chatContent.scrollTop = chatContent.scrollHeight;
        } else {
            toastr.error(result.message || 'Không thể lưu nội dung.');
        }
    } catch (error) {
        console.error('Error uploading to cloud:', error);
        toastr.error('Đã xảy ra lỗi khi lưu nội dung vào Cloud.');
    }
}

init();