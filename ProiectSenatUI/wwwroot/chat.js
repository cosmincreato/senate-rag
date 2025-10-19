function scrollChatToBottom() {
    const chatMessages = document.getElementById("chat-messages");
    if (chatMessages) {
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }
}

// Auto-scroll when new messages are added
window.addEventListener("DOMContentLoaded", function() {
    // Create a MutationObserver to watch for new messages
    const chatMessages = document.getElementById("chat-messages");
    if (chatMessages) {
        const observer = new MutationObserver(function() {
            scrollChatToBottom();
        });

        observer.observe(chatMessages, {
            childList: true,
            subtree: true
        });
    }
});