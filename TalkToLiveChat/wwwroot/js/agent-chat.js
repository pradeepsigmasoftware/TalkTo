/**
 * TalkTo Live Chat - Support Agent Workspace & Real-Time Engine
 */

(function () {
    'use strict';

    let currentAgentId = 0;
    let currentAgentName = '';
    let selectedSessionId = null;
    let selectedSessionData = null;
    let connection = null;
    let typingTimer = null;
    let currentTab = 'active'; // 'active' or 'pending' or 'completed'
    let allSessions = { active: [], pending: [], completed: [] };

    // Pleasant Sound Generator
    function playAgentChime() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            const ctx = new AudioContext();

            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'triangle';
            osc.frequency.setValueAtTime(523.25, ctx.currentTime); // C5
            osc.frequency.setValueAtTime(659.25, ctx.currentTime + 0.08); // E5
            osc.frequency.setValueAtTime(783.99, ctx.currentTime + 0.16); // G5

            gain.gain.setValueAtTime(0.2, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.45);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start();
            osc.stop(ctx.currentTime + 0.45);
        } catch (e) {}
    }

    // Initialize Agent Workspace
    window.initAgentWorkspace = function (agentId, agentName) {
        currentAgentId = agentId;
        currentAgentName = agentName;

        setupDOMEvents();
        setupSignalR();
        loadDashboardStats();
        loadSessions();
    };

    function setupDOMEvents() {
        // Tab switching
        document.querySelectorAll('.chat-list-tab').forEach(tab => {
            tab.addEventListener('click', function () {
                document.querySelectorAll('.chat-list-tab').forEach(t => t.classList.remove('active'));
                this.classList.add('active');
                currentTab = this.getAttribute('data-tab');
                renderSessionsList();
            });
        });

        // Search in chat list
        const searchInput = document.getElementById('chatSearchInput');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                renderSessionsList(this.value.trim().toLowerCase());
            });
        }

        // Status dropdown change
        const statusSelect = document.getElementById('agentStatusSelect');
        if (statusSelect) {
            statusSelect.addEventListener('change', async function () {
                const newStatus = this.value;
                this.className = `dash-status-select ${newStatus.toLowerCase()}`;
                try {
                    await fetch('/Agent/UpdateAvailability', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(newStatus)
                    });
                } catch (e) {}
            });
        }

        // Message input enter key
        const msgInput = document.getElementById('agentMessageInput');
        if (msgInput) {
            msgInput.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    sendAgentMessage();
                }
            });

            // Typing indicator
            msgInput.addEventListener('input', function () {
                if (!connection || !selectedSessionId) return;
                connection.invoke('SendTyping', selectedSessionId, currentAgentName, 'Agent', true).catch(() => {});
                clearTimeout(typingTimer);
                typingTimer = setTimeout(() => {
                    connection.invoke('SendTyping', selectedSessionId, currentAgentName, 'Agent', false).catch(() => {});
                }, 1800);
            });
        }

        const btnSend = document.getElementById('btnAgentSend');
        if (btnSend) {
            btnSend.addEventListener('click', sendAgentMessage);
        }

        // Agent Attachment Upload
        const btnAttach = document.getElementById('btnAgentAttach');
        const fileInput = document.getElementById('agentFileInput');
        if (btnAttach && fileInput) {
            btnAttach.addEventListener('click', () => fileInput.click());
            fileInput.addEventListener('change', async () => {
                if (!fileInput.files || fileInput.files.length === 0 || !selectedSessionId) return;
                const file = fileInput.files[0];

                const formData = new FormData();
                formData.append('file', file);
                formData.append('chatSessionId', selectedSessionId);
                formData.append('senderId', currentAgentId.toString());
                formData.append('senderType', 'Agent');
                formData.append('senderName', currentAgentName);

                btnAttach.disabled = true;
                btnAttach.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i>';

                try {
                    const res = await fetch('/api/chatwidget/upload-attachment', {
                        method: 'POST',
                        body: formData
                    });
                    const data = await res.json();
                    if (!data.success) {
                        alert(data.message || 'Upload failed.');
                    }
                } catch (e) {
                    alert('Upload failed.');
                } finally {
                    btnAttach.disabled = false;
                    btnAttach.innerHTML = '<i class="fa-solid fa-paperclip"></i>';
                    fileInput.value = '';
                }
            });
        }

        // Transfer Chat Button
        const btnTransfer = document.getElementById('btnTransferChat');
        if (btnTransfer) {
            btnTransfer.addEventListener('click', openTransferModal);
        }

        // Add Note Button
        const btnAddNote = document.getElementById('btnAddNote');
        const noteInput = document.getElementById('agentNoteInput');
        if (btnAddNote && noteInput) {
            btnAddNote.addEventListener('click', async () => {
                const text = noteInput.value.trim();
                if (!text || !selectedSessionId) return;

                btnAddNote.disabled = true;
                try {
                    const res = await fetch('/Agent/AddNote', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            chatSessionId: selectedSessionId,
                            noteText: text
                        })
                    });
                    const data = await res.json();
                    if (data.success && data.note) {
                        appendInternalNote(data.note);
                        noteInput.value = '';
                    }
                } catch (e) {
                    alert('Failed to save note.');
                } finally {
                    btnAddNote.disabled = false;
                }
            });
        }

        // Close Chat Button
        const btnClose = document.getElementById('btnCloseChat');
        if (btnClose) {
            btnClose.addEventListener('click', async () => {
                if (!selectedSessionId) return;
                if (!confirm('Are you sure you want to end and complete this chat session?')) return;

                try {
                    await fetch('/Agent/CloseSession', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(selectedSessionId)
                    });
                    loadDashboardStats();
                    loadSessions();
                } catch (e) {
                    alert('Error closing session.');
                }
            });
        }
    }

    function setupSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/chatHub')
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // New Message Received
        connection.on('ReceiveMessage', (msg) => {
            if (msg.chatSessionId === selectedSessionId) {
                appendAgentConvoBubble(msg);
                scrollConvoBottom();
                if (msg.senderType === 'Customer') {
                    // Mark as read immediately since conversation is open
                    connection.invoke('MarkAsRead', selectedSessionId, 'Agent').catch(() => {});
                }
            } else {
                playAgentChime();
            }
            loadDashboardStats();
            loadSessions(false); // background refresh list
        });

        // Customer Typing
        connection.on('UserTyping', (data) => {
            if (data.chatSessionId === selectedSessionId && data.senderType === 'Customer') {
                const typingElem = document.getElementById('agentTypingIndicator');
                if (typingElem) {
                    typingElem.style.display = data.isTyping ? 'block' : 'none';
                    if (data.isTyping) scrollConvoBottom();
                }
            }
        });

        // Messages Read
        connection.on('MessagesRead', (data) => {
            if (data.chatSessionId === selectedSessionId && data.readerType === 'Customer') {
                document.querySelectorAll('.agent-view-row.agent .talkto-ticks').forEach(el => {
                    el.className = 'talkto-ticks read';
                    el.innerHTML = '✓✓';
                });
            }
        });

        // New Incoming Chat
        connection.on('NewIncomingChat', (data) => {
            playAgentChime();
            loadDashboardStats();
            loadSessions();
        });

        connection.on('ChatStatusChanged', () => {
            loadDashboardStats();
            loadSessions();
        });

        connection.on('ChatTransferredNotice', () => {
            loadDashboardStats();
            loadSessions();
        });

        connection.on('ChatClosedNotice', () => {
            loadDashboardStats();
            loadSessions();
        });

        connection.start().then(() => {
            // Join Agent room
            connection.invoke('JoinSession', 'global_agent', 'Agent', currentAgentId.toString(), currentAgentName)
                .catch(err => console.error('Join agents room error:', err));
        }).catch(err => console.error('SignalR start error:', err));
    }

    async function loadDashboardStats() {
        try {
            const res = await fetch('/Agent/GetDashboardStats');
            const stats = await res.json();
            if (stats) {
                document.getElementById('statTotalAssigned').textContent = stats.totalAssignedChats;
                document.getElementById('statActiveChats').textContent = stats.activeChats;
                document.getElementById('statPendingChats').textContent = stats.pendingChats;
                document.getElementById('statCompletedChats').textContent = stats.completedChats;
                document.getElementById('statUnreadMessages').textContent = stats.unreadMessages;

                const pendingTabBadge = document.getElementById('pendingTabBadge');
                if (pendingTabBadge) {
                    pendingTabBadge.textContent = stats.pendingChats;
                    pendingTabBadge.style.display = stats.pendingChats > 0 ? 'inline-block' : 'none';
                }
            }
        } catch (e) {}
    }

    async function loadSessions(shouldSelectFirst = true) {
        try {
            const res = await fetch('/Agent/GetSessions');
            const data = await res.json();
            if (data) {
                allSessions = data;
                renderSessionsList();

                // If currently selected session exists, refresh info
                if (selectedSessionId) {
                    const currentActive = allSessions.active.find(s => s.chatSessionId === selectedSessionId);
                    if (currentActive) {
                        renderCustomerDetails(currentActive);
                    }
                } else if (shouldSelectFirst && allSessions.active.length > 0) {
                    selectSession(allSessions.active[0].chatSessionId);
                }
            }
        } catch (e) {}
    }

    function renderSessionsList(filterText = '') {
        const container = document.getElementById('chatItemsContainer');
        if (!container) return;

        let list = allSessions[currentTab] || [];
        if (filterText) {
            list = list.filter(s => 
                (s.mobileNumber && s.mobileNumber.toLowerCase().includes(filterText)) ||
                (s.serviceName && s.serviceName.toLowerCase().includes(filterText)) ||
                (s.chatSessionId && s.chatSessionId.toLowerCase().includes(filterText))
            );
        }

        if (list.length === 0) {
            container.innerHTML = `
                <div class="text-center p-4 text-muted" style="font-size:13px;">
                    <i class="fa-regular fa-comment-dots mb-2" style="font-size:24px;"></i>
                    <div>No ${currentTab} chats available</div>
                </div>
            `;
            return;
        }

        container.innerHTML = '';
        list.forEach(item => {
            const isSelected = item.chatSessionId === selectedSessionId;
            const card = document.createElement('div');
            card.className = `chat-item-card ${isSelected ? 'active' : ''}`;
            card.setAttribute('data-id', item.chatSessionId);

            const timeStr = formatShortTime(item.lastMessageDate || item.startDate);
            const isPending = currentTab === 'pending';

            card.innerHTML = `
                <div class="chat-item-avatar">
                    <i class="fa-solid fa-user"></i>
                </div>
                <div class="chat-item-body">
                    <div class="chat-item-header">
                        <span class="chat-item-mobile">${escapeHtml(item.countryCode)} ${escapeHtml(item.mobileNumber)}</span>
                        <span class="chat-item-time">${timeStr}</span>
                    </div>
                    <div class="chat-item-preview">${escapeHtml(item.lastMessageText || 'Chat started')}</div>
                    <div class="chat-item-tags">
                        <span class="service-badge">${escapeHtml(item.serviceName)}</span>
                        ${item.unreadCustomerMessages > 0 ? `<span class="badge bg-danger rounded-pill">${item.unreadCustomerMessages}</span>` : ''}
                        ${isPending ? `<button class="btn btn-sm btn-success py-0 px-2 btn-accept-pending" data-id="${item.chatSessionId}" style="font-size:11px;">Accept</button>` : ''}
                    </div>
                </div>
            `;

            card.addEventListener('click', (e) => {
                if (e.target.classList.contains('btn-accept-pending')) {
                    e.stopPropagation();
                    acceptPendingChat(item.chatSessionId);
                } else {
                    selectSession(item.chatSessionId);
                }
            });

            container.appendChild(card);
        });
    }

    async function selectSession(sessionId) {
        if (!sessionId) return;
        selectedSessionId = sessionId;

        // Highlight active card
        document.querySelectorAll('.chat-item-card').forEach(c => {
            c.classList.toggle('active', c.getAttribute('data-id') === sessionId);
        });

        // Join SignalR group for real-time messages
        if (connection) {
            connection.invoke('JoinSession', sessionId, 'Agent', currentAgentId.toString(), currentAgentName).catch(() => {});
        }

        try {
            const res = await fetch(`/Agent/GetSessionDetails?sessionId=${encodeURIComponent(sessionId)}`);
            const data = await res.json();
            if (data && data.session) {
                selectedSessionData = data.session;
                renderConversationHeader(data.session);
                renderConversationMessages(data.messages || []);
                renderCustomerDetails(data.session);
                renderInternalNotes(data.notes || []);

                // Enable composer
                const msgInput = document.getElementById('agentMessageInput');
                const btnSend = document.getElementById('btnAgentSend');
                if (msgInput) {
                    msgInput.disabled = (data.session.sessionStatus === 'Completed' || data.session.sessionStatus === 'Closed');
                    msgInput.focus();
                }
                if (btnSend) {
                    btnSend.disabled = (data.session.sessionStatus === 'Completed' || data.session.sessionStatus === 'Closed');
                }
            }
        } catch (e) {
            console.error('Error fetching session details:', e);
        }
    }

    async function acceptPendingChat(sessionId) {
        try {
            const res = await fetch('/Agent/AcceptChat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(sessionId)
            });
            const data = await res.json();
            if (data.success) {
                currentTab = 'active';
                document.querySelectorAll('.chat-list-tab').forEach(t => {
                    t.classList.toggle('active', t.getAttribute('data-tab') === 'active');
                });
                await loadSessions();
                selectSession(sessionId);
            }
        } catch (e) {
            alert('Could not accept chat.');
        }
    }

    function renderConversationHeader(session) {
        document.getElementById('convoCustomerMobile').textContent = `${session.countryCode} ${session.mobileNumber}`;
        document.getElementById('convoServiceName').textContent = session.serviceName;
        document.getElementById('convoSessionId').textContent = session.chatSessionId;
        
        const badge = document.getElementById('convoStatusBadge');
        if (badge) {
            badge.textContent = session.sessionStatus;
            badge.className = `badge ${session.sessionStatus === 'Active' ? 'bg-success' : (session.sessionStatus === 'Pending' ? 'bg-warning text-dark' : 'bg-secondary')}`;
        }
    }

    function renderConversationMessages(messages) {
        const stream = document.getElementById('agentConversationStream');
        if (!stream) return;

        stream.innerHTML = '<div class="talkto-date-divider"><span>Conversation History</span></div>';
        messages.forEach(msg => {
            appendAgentConvoBubble(msg);
        });
        scrollConvoBottom();
    }

    function appendAgentConvoBubble(msg) {
        const stream = document.getElementById('agentConversationStream');
        if (!stream) return;

        const row = document.createElement('div');
        const isAgent = msg.senderType === 'Agent';
        const isSystem = msg.senderType === 'System';

        row.className = `agent-view-row ${isAgent ? 'agent' : (isSystem ? 'system' : 'customer')}`;

        let content = '';
        if (msg.messageType === 'Image' && msg.filePath) {
            content += `<img src="${msg.filePath}" alt="Image" style="max-width:240px; border-radius:8px; margin-bottom:6px; cursor:pointer;" onclick="window.open('${msg.filePath}', '_blank')" />`;
        } else if (msg.messageType === 'File' && msg.filePath) {
            content += `
                <a href="${msg.filePath}" target="_blank" download class="talkto-bubble-file" style="color:inherit;">
                    <i class="fa-solid fa-file-arrow-down"></i>
                    <div>
                        <div style="font-weight:600; font-size:12px;">${escapeHtml(msg.fileName || 'Attachment')}</div>
                        <div style="font-size:10px; opacity:0.8;">Click to download</div>
                    </div>
                </a>
            `;
        }

        content += `<div>${escapeHtml(msg.messageText || '')}</div>`;

        if (!isSystem) {
            const timeStr = formatShortTime(msg.sentDate);
            const checkmarks = isAgent ? `<span class="talkto-ticks ${msg.isRead ? 'read' : ''}" style="color:${isAgent ? '#ffffff' : '#2563EB'}; opacity:0.8;">✓✓</span>` : '';
            content += `
                <div class="agent-bubble-meta">
                    <span>${timeStr}</span>
                    ${checkmarks}
                </div>
            `;
        }

        row.innerHTML = `<div class="agent-bubble">${content}</div>`;
        stream.appendChild(row);
    }

    function renderCustomerDetails(session) {
        document.getElementById('infoMobile').textContent = `${session.countryCode} ${session.mobileNumber}`;
        document.getElementById('infoService').textContent = session.serviceName;
        document.getElementById('infoSessionId').textContent = session.chatSessionId;
        document.getElementById('infoStartTime').textContent = new Date(session.startDate).toLocaleString();
        document.getElementById('infoTotalMessages').textContent = session.totalMessages;
    }

    function renderInternalNotes(notes) {
        const list = document.getElementById('internalNotesList');
        if (!list) return;

        list.innerHTML = '';
        if (notes.length === 0) {
            list.innerHTML = '<div class="text-muted" style="font-size:12px;">No internal notes yet.</div>';
            return;
        }

        notes.forEach(note => appendInternalNote(note));
    }

    function appendInternalNote(note) {
        const list = document.getElementById('internalNotesList');
        if (!list) return;

        // Clear empty state if present
        if (list.querySelector('.text-muted')) {
            list.innerHTML = '';
        }

        const item = document.createElement('div');
        item.className = 'note-item';
        item.innerHTML = `
            <div class="note-item-meta">${escapeHtml(note.agentName)} • ${formatShortTime(note.createdDate)}</div>
            <div>${escapeHtml(note.noteText)}</div>
        `;
        list.prepend(item);
    }

    async function sendAgentMessage() {
        const input = document.getElementById('agentMessageInput');
        const text = input.value.trim();
        if (!text || !selectedSessionId) return;

        input.value = '';
        input.focus();

        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            try {
                await connection.invoke(
                    'SendMessage',
                    selectedSessionId,
                    currentAgentId.toString(),
                    currentAgentName,
                    'Agent',
                    text,
                    'Text',
                    null,
                    null,
                    null
                );
            } catch (err) {
                console.error('Agent SendMessage error:', err);
            }
        }
    }

    async function openTransferModal() {
        if (!selectedSessionId) return;

        try {
            const res = await fetch('/Agent/GetTransferAgents');
            const agents = await res.json();
            const select = document.getElementById('transferAgentSelect');
            select.innerHTML = '<option value="">-- Choose agent --</option>';

            agents.forEach(a => {
                const opt = document.createElement('option');
                opt.value = a.agentId;
                opt.textContent = `${a.agentName} (${a.availabilityStatus} - ${a.activeChatsCount}/${a.maxConcurrentChats} chats)`;
                select.appendChild(opt);
            });

            const modal = new bootstrap.Modal(document.getElementById('transferChatModal'));
            modal.show();
        } catch (e) {
            alert('Failed to load agents for transfer.');
        }
    }

    window.confirmTransferChat = async function () {
        const select = document.getElementById('transferAgentSelect');
        const targetId = parseInt(select.value, 10);
        if (!targetId || !selectedSessionId) {
            alert('Please select an agent.');
            return;
        }

        try {
            const res = await fetch('/Agent/TransferChat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    chatSessionId: selectedSessionId,
                    targetAgentId: targetId
                })
            });
            const data = await res.json();
            if (data.success) {
                bootstrap.Modal.getInstance(document.getElementById('transferChatModal')).hide();
                alert('Chat transferred successfully.');
                loadDashboardStats();
                loadSessions();
            } else {
                alert(data.message || 'Transfer failed.');
            }
        } catch (e) {
            alert('Transfer failed.');
        }
    };

    function scrollConvoBottom() {
        const stream = document.getElementById('agentConversationStream');
        if (stream) stream.scrollTop = stream.scrollHeight;
    }

    function formatShortTime(dateVal) {
        if (!dateVal) return '';
        const d = new Date(dateVal);
        let hours = d.getHours();
        const mins = d.getMinutes();
        const ampm = hours >= 12 ? 'pm' : 'am';
        hours = hours % 12 || 12;
        return `${hours}:${mins < 10 ? '0' : ''}${mins} ${ampm}`;
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
})();
