/**
 * TalkTo Live Chat - Admin Dashboard & Analytics Engine
 */

(function () {
    'use strict';

    let serviceChartInstance = null;
    let volumeChartInstance = null;
    let monitoringConnection = null;

    window.initAdminDashboard = function () {
        setupMonitoringSignalR();
        loadKPIs();
        loadMonitoringSessions();
        initCharts();

        // Bind filter events
        const btnFilter = document.getElementById('btnApplyFilters');
        if (btnFilter) {
            btnFilter.addEventListener('click', loadMonitoringSessions);
        }

        const searchMobile = document.getElementById('filterSearchMobile');
        if (searchMobile) {
            searchMobile.addEventListener('keyup', (e) => {
                if (e.key === 'Enter') loadMonitoringSessions();
            });
        }
    };

    function setupMonitoringSignalR() {
        monitoringConnection = new signalR.HubConnectionBuilder()
            .withUrl('/chatHub')
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        monitoringConnection.on('ReceiveMessage', () => {
            loadKPIs();
            loadMonitoringSessions();
        });

        monitoringConnection.on('NewIncomingChat', () => {
            loadKPIs();
            loadMonitoringSessions();
        });

        monitoringConnection.on('ChatStatusChanged', () => {
            loadKPIs();
            loadMonitoringSessions();
        });

        monitoringConnection.on('AgentAvailabilityChanged', () => {
            loadKPIs();
        });

        monitoringConnection.start().then(() => {
            monitoringConnection.invoke('JoinSession', 'admin_room', 'Admin', '1', 'Administrator')
                .catch(err => console.error(err));
        }).catch(err => console.error(err));
    }

    async function loadKPIs() {
        try {
            const res = await fetch('/Admin/GetDashboardStats');
            const stats = await res.json();
            if (stats) {
                document.getElementById('adminStatVisitors').textContent = stats.totalVisitors;
                document.getElementById('adminStatTotalChats').textContent = stats.totalChats;
                document.getElementById('adminStatActive').textContent = stats.activeChats;
                document.getElementById('adminStatPending').textContent = stats.pendingChats;
                document.getElementById('adminStatCompleted').textContent = stats.completedChats;
                document.getElementById('adminStatOnlineAgents').textContent = stats.onlineAgents;
                document.getElementById('adminStatOfflineAgents').textContent = stats.offlineAgents;

                updateCharts(stats.serviceStats);
            }
        } catch (e) {}
    }

    async function loadMonitoringSessions() {
        const serviceId = document.getElementById('filterService')?.value || '';
        const status = document.getElementById('filterStatus')?.value || '';
        const searchMobile = document.getElementById('filterSearchMobile')?.value || '';
        const startDate = document.getElementById('filterStartDate')?.value || '';
        const endDate = document.getElementById('filterEndDate')?.value || '';

        const url = `/Admin/GetSessions?serviceId=${encodeURIComponent(serviceId)}&status=${encodeURIComponent(status)}&searchMobile=${encodeURIComponent(searchMobile)}&startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;

        try {
            const res = await fetch(url);
            const sessions = await res.json();
            renderMonitoringTable(sessions);
        } catch (e) {
            console.error('Error loading monitoring sessions:', e);
        }
    }

    function renderMonitoringTable(sessions) {
        const tbody = document.getElementById('monitoringTableBody');
        if (!tbody) return;

        if (!sessions || sessions.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" class="text-center text-muted p-4">No chat sessions match the criteria.</td></tr>';
            return;
        }

        tbody.innerHTML = '';
        sessions.forEach(s => {
            const tr = document.createElement('tr');

            const statusClass = s.sessionStatus === 'Active' ? 'bg-success' : (s.sessionStatus === 'Pending' ? 'bg-warning text-dark' : 'bg-secondary');
            const startTimeStr = new Date(s.startDate).toLocaleString();

            tr.innerHTML = `
                <td><code style="font-size:12px; font-weight:700;">${escapeHtml(s.chatSessionId)}</code></td>
                <td><strong>${escapeHtml(s.countryCode)} ${escapeHtml(s.mobileNumber)}</strong></td>
                <td><span class="service-badge">${escapeHtml(s.serviceName)}</span></td>
                <td>
                    ${s.agentName ? `
                        <div class="d-flex align-items-center gap-2">
                            <span class="talkto-status-dot" style="background:${s.agentStatus === 'Online' ? '#10B981' : '#94A3B8'}"></span>
                            <span>${escapeHtml(s.agentName)}</span>
                        </div>
                    ` : '<span class="text-muted fst-italic">Unassigned</span>'}
                </td>
                <td><span class="badge ${statusClass}">${escapeHtml(s.sessionStatus)}</span></td>
                <td style="font-size:12px;">${startTimeStr}</td>
                <td><span class="badge bg-light text-dark border">${s.totalMessages} msgs</span></td>
                <td>
                    <button class="btn btn-sm btn-outline-primary py-0 px-2 btn-view-chat" data-id="${s.chatSessionId}" style="font-size:12px;">
                        <i class="fa-regular fa-eye me-1"></i> View
                    </button>
                    ${s.sessionStatus === 'Pending' || s.sessionStatus === 'Active' ? `
                        <button class="btn btn-sm btn-outline-danger py-0 px-2 btn-admin-close" data-id="${s.chatSessionId}" style="font-size:12px;">
                            <i class="fa-solid fa-power-off"></i>
                        </button>
                    ` : ''}
                </td>
            `;

            tr.querySelector('.btn-view-chat').addEventListener('click', () => {
                viewChatTranscriptModal(s.chatSessionId);
            });

            const closeBtn = tr.querySelector('.btn-admin-close');
            if (closeBtn) {
                closeBtn.addEventListener('click', async () => {
                    if (confirm('Are you sure you want to close this session?')) {
                        await fetch('/Admin/CloseSession', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(s.chatSessionId)
                        });
                        loadMonitoringSessions();
                        loadKPIs();
                    }
                });
            }

            tbody.appendChild(tr);
        });
    }

    async function viewChatTranscriptModal(sessionId) {
        try {
            const res = await fetch(`/Admin/GetSessionDetails?sessionId=${encodeURIComponent(sessionId)}`);
            const data = await res.json();
            if (data && data.session) {
                document.getElementById('transcriptSessionId').textContent = data.session.chatSessionId;
                document.getElementById('transcriptMobile').textContent = `${data.session.countryCode} ${data.session.mobileNumber}`;
                document.getElementById('transcriptService').textContent = data.session.serviceName;
                document.getElementById('transcriptAgent').textContent = data.session.agentName || 'Unassigned';

                const stream = document.getElementById('transcriptMessagesStream');
                stream.innerHTML = '';

                (data.messages || []).forEach(m => {
                    const isCust = m.senderType === 'Customer';
                    const isSys = m.senderType === 'System';
                    const div = document.createElement('div');
                    div.className = `talkto-msg-row ${isCust ? 'customer' : (isSys ? 'system' : 'agent')}`;
                    div.innerHTML = `
                        <div class="talkto-bubble">
                            <div style="font-size:11px; font-weight:700; margin-bottom:2px; opacity:0.8;">${escapeHtml(m.senderName || m.senderType)}</div>
                            <div>${escapeHtml(m.messageText)}</div>
                            <div class="talkto-bubble-meta">${new Date(m.sentDate).toLocaleTimeString()}</div>
                        </div>
                    `;
                    stream.appendChild(div);
                });

                const modal = new bootstrap.Modal(document.getElementById('chatTranscriptModal'));
                modal.show();
            }
        } catch (e) {
            alert('Failed to load chat details.');
        }
    }

    function initCharts() {
        const serviceCtx = document.getElementById('servicePieChart')?.getContext('2d');
        if (serviceCtx) {
            serviceChartInstance = new Chart(serviceCtx, {
                type: 'doughnut',
                data: {
                    labels: [],
                    datasets: [{
                        data: [],
                        backgroundColor: [
                            '#059669', '#2563EB', '#7C3AED', '#D97706', '#DC2626',
                            '#0D9488', '#4F46E5', '#EA580C', '#64748B'
                        ]
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'right' }
                    }
                }
            });
        }
    }

    function updateCharts(serviceStats) {
        if (!serviceChartInstance || !serviceStats) return;

        const labels = serviceStats.map(s => s.serviceName);
        const data = serviceStats.map(s => s.totalChats);

        serviceChartInstance.data.labels = labels;
        serviceChartInstance.data.datasets[0].data = data;
        serviceChartInstance.update();
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
})();
