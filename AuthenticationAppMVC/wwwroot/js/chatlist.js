const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

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
        } else {
            // We're not viewing this group, show notification
            const groupName = document.querySelector(`.group-item[data-group-id="${groupId}"]`)?.getAttribute("data-name") || "Group";
            const displayName = senderName || senderEmail.split("@")[0];
            showNotification(`${displayName} đã nhắc bạn từ (${groupName})`, `${message} [Đã gửi file]`);
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
            selectContact(this);
        });
    });

    // Group creation
    btnNewGroup.addEventListener("click", () => {
        groupNameInput.value = "";
        groupDescriptionInput.value = "";
        modalCreateGroup.style.display = "block";
    });

    // Close modals
    document.querySelectorAll(".close").forEach(closeBtn => {
        closeBtn.addEventListener("click", function () {
            modalCreateGroup.style.display = "none";
            modalGroupMembers.style.display = "none";
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
    });
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
    selectedContact = contactElement.getAttribute("data-email");
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
                appendMessageWithAttachments(msg.senderEmail, msg.content, msg.timestamp, msg.attachments);
            } else {
                appendMessage(msg.senderEmail, msg.content, msg.timestamp);
            }
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
                appendMessageWithAttachments(msg.senderEmail, msg.content, msg.timestamp, msg.attachments);
            } else {
                appendMessage(msg.senderEmail, msg.content, msg.timestamp);
            }
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
        if (currentChatType === "contact" && currentReceiverId) {
            if (selectedFiles.length > 0) {
                // Send message with file
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

function appendMessage(senderEmail, message, timestamp) {
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
    if (!isSentByMe) {
        div.innerHTML = `
            <div class="sender-info">
                <img class="avatar" src="/img/user_ava.svg" alt="Avatar">
                <span class="sender-name">${senderName}</span>
            </div>
            <div class="message-content">
                <p>${message}</p>
                <span class="timestamp">${localTimestamp}</span>
            </div>
        `;
    } else {
        div.innerHTML = `
            <div class="message-content">
                <p>${message}</p>
                <span class="timestamp">${localTimestamp}</span>
            </div>
        `;
    }
    chatContent.appendChild(div);
    chatContent.scrollTop = chatContent.scrollHeight;
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

    if (!isSentByMe) {
        div.innerHTML = `
            <div class="sender-info">
                <img class="avatar" src="/img/user_ava.svg" alt="Avatar">
                <span class="sender-name">${senderName}</span>
            </div>
            <div class="message-content">
                <p>${message}</p>
                ${attachmentsHtml}
                <span class="timestamp">${localTimestamp}</span>
            </div>
        `;
    } else {
        div.innerHTML = `
            <div class="message-content">
                <p>${message}</p>
                ${attachmentsHtml}
                <span class="timestamp">${localTimestamp}</span>
            </div>
        `;
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

init();