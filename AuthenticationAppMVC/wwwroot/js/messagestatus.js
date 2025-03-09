/**
 * Message Status Handler for Zilo App
 * Handles message status indicators for personal and group chats
 */

// Message status enum to match backend
window.MessageStatus = {
    Sent: 0,
    Delivered: 1,
    Read: 2
};

// Status icon mapping
window.StatusIcons = {
    [MessageStatus.Sent]: '<i class="fas fa-check text-secondary"></i>',
    [MessageStatus.Delivered]: '<i class="fas fa-check-double text-secondary"></i>',
    [MessageStatus.Read]: '<i class="fas fa-check-double text-primary"></i>'
};

// Initialize message status tracking
function initMessageStatusTracking() {
    console.log("Initializing message status tracking");

    // Update status for a single direct message
    connection.on("MessageStatusUpdated", (messageId, status) => {
        console.log(`Message ${messageId} status updated to ${status}`);
        updateMessageStatus(messageId, status);
    });

    // Update status for multiple direct messages
    connection.on("MessagesRead", (messageIds) => {
        console.log(`Messages read: ${messageIds.join(', ')}`);
        messageIds.forEach(id => {
            updateMessageStatus(id, MessageStatus.Read);
        });
    });

    // Update status for a group message
    connection.on("GroupMessageStatusUpdated", (messageId, status) => {
        console.log(`Group message ${messageId} status updated to ${status}`);
        updateMessageStatus(messageId, status);
    });

    // Update UI when someone reads a group message
    connection.on("GroupMessageRead", (messageId, userId, userEmail) => {
        console.log(`Group message ${messageId} read by ${userEmail}`);
        // This could update a tooltip showing who has read the message
        updateGroupMessageReadStatus(messageId, userId, userEmail);
    });

    // Update UI when a user reads all messages in a group
    connection.on("GroupMessagesReadByUser", (groupId, userId, userEmail) => {
        console.log(`All messages in group ${groupId} read by ${userEmail}`);
        // Could update group chat unread indicators
        updateGroupUnreadStatus(groupId);
    });
}

// Update message status in UI
window.updateMessageStatus = function (messageId, status) {
    const statusElement = document.querySelector(`.message-status[data-message-id="${messageId}"]`);
    if (statusElement) {
        statusElement.innerHTML = StatusIcons[status];
        statusElement.setAttribute('data-status', status);

        // Update tooltip
        switch (parseInt(status)) {
            case MessageStatus.Sent:
                statusElement.setAttribute('title', 'Đã gửi');
                break;
            case MessageStatus.Delivered:
                statusElement.setAttribute('title', 'Đã nhận');
                break;
            case MessageStatus.Read:
                statusElement.setAttribute('title', 'Đã đọc');
                break;
        }
    }
};

// Update group message read status (who has read)
function updateGroupMessageReadStatus(messageId, userId, userEmail) {
    const statusElement = document.querySelector(`.message-status[data-message-id="${messageId}"]`);
    if (statusElement) {
        // Update to read status icon
        updateMessageStatus(messageId, MessageStatus.Read);

        // Add user to read-by list (for tooltip)
        let readBy = statusElement.getAttribute('data-read-by') || '';
        if (readBy) {
            try {
                let readers = JSON.parse(readBy);
                if (!readers.find(r => r.id === userId)) {
                    readers.push({ id: userId, email: userEmail });
                    statusElement.setAttribute('data-read-by', JSON.stringify(readers));

                    // Update tooltip to show who read the message
                    const readerNames = readers.map(r => r.email.split('@')[0]).join(', ');
                    statusElement.setAttribute('title', `Đã đọc bởi: ${readerNames}`);
                }
            } catch (e) {
                console.error('Error parsing readers', e);
                statusElement.setAttribute('data-read-by', JSON.stringify([{ id: userId, email: userEmail }]));
                statusElement.setAttribute('title', `Đã đọc bởi: ${userEmail.split('@')[0]}`);
            }
        } else {
            statusElement.setAttribute('data-read-by', JSON.stringify([{ id: userId, email: userEmail }]));
            statusElement.setAttribute('title', `Đã đọc bởi: ${userEmail.split('@')[0]}`);
        }
    }
}

// Update group unread status (e.g., badge count)
function updateGroupUnreadStatus(groupId) {
    const groupElement = document.querySelector(`.group-item[data-group-id="${groupId}"]`);
    if (groupElement) {
        const unreadBadge = groupElement.querySelector('.unread-badge');
        if (unreadBadge) {
            unreadBadge.textContent = '0';
            unreadBadge.classList.add('d-none');
        }
    }
}

// Mark messages as read when chat is opened
function markMessagesAsReadWhenVisible(recipientEmail = null, groupId = null) {
    // For direct messages
    if (recipientEmail) {
        connection.invoke("MarkAllMessagesAsRead", recipientEmail)
            .catch(err => console.error(`Error marking messages as read: ${err.toString()}`));
    }

    // For group messages
    if (groupId) {
        connection.invoke("MarkAllGroupMessagesAsRead", groupId)
            .catch(err => console.error(`Error marking group messages as read: ${err.toString()}`));
    }
}

// Mark individual message as read when it becomes visible
function markMessageAsRead(messageId, isGroupMessage = false) {
    if (isGroupMessage) {
        connection.invoke("MarkGroupMessageAsRead", messageId)
            .catch(err => console.error(`Error marking group message as read: ${err.toString()}`));
    } else {
        connection.invoke("MarkMessageAsRead", messageId)
            .catch(err => console.error(`Error marking message as read: ${err.toString()}`));
    }
}

// Create message status element to append to messages
window.createMessageStatusElement = function (messageId, status, isOwnMessage) {
    // Only show status for messages sent by the current user
    if (!isOwnMessage) return '';

    return `<span class="message-status" 
                 data-message-id="${messageId}" 
                 data-status="${status}" 
                 title="${getStatusText(status)}">
                ${StatusIcons[status]}
            </span>`;
};

// Get text representation of status
window.getStatusText = function (status) {
    status = parseInt(status || 0);
    switch (status) {
        case window.MessageStatus.Sent: return 'Đã gửi';
        case window.MessageStatus.Delivered: return 'Đã nhận';
        case window.MessageStatus.Read: return 'Đã đọc';
        default: return 'Không xác định';
    }
};

// Initialize when document is ready
document.addEventListener('DOMContentLoaded', function () {
    // Make sure the connection is established first
    if (typeof connection !== 'undefined') {
        if (connection.state === signalR.HubConnectionState.Connected) {
            initMessageStatusTracking();
        } else {
            connection.onreconnected = function () {
                initMessageStatusTracking();
            };
        }
    }
});