/**
 * Tawk.to-Style Web-Based Live Chat Application Widget
 * Fully responsive, real-time SignalR powered, session persistent
 */

(function () {
    'use strict';

    // State Variables
    let currentSessionId = localStorage.getItem('talkto_session_id') || null;
    let currentVisitorId = parseInt(localStorage.getItem('talkto_visitor_id') || '0', 10);
    let currentMobile = localStorage.getItem('talkto_mobile') || '';
    let currentCountryCode = localStorage.getItem('talkto_country_code') || '+91';
    let currentServiceId = parseInt(localStorage.getItem('talkto_service_id') || '0', 10);
    let currentServiceName = localStorage.getItem('talkto_service_name') || '';
    let assignedAgent = null;
    let isConnected = false;
    let typingTimeout = null;
    let unreadCount = 0;
    let selectedRating = 5;

    // SignalR Connection
    let connection = null;

    // Web Audio Sound Generator (Pleasant Chime)
    function playNotificationSound() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            const ctx = new AudioContext();

            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'sine';
            osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
            osc.frequency.setValueAtTime(880, ctx.currentTime + 0.1); // A5

            gain.gain.setValueAtTime(0.15, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.35);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start();
            osc.stop(ctx.currentTime + 0.35);
        } catch (e) {
            // Audio context might be restricted before user gesture
        }
    }

    // Initialize Widget DOM
    function initWidget() {
        if (document.getElementById('talkto-widget-root')) return;

        const widgetContainer = document.createElement('div');
        widgetContainer.id = 'talkto-widget-root';
        widgetContainer.className = 'talkto-widget-container';
        widgetContainer.innerHTML = `
            <!-- Chat Window -->
            <div id="talktoChatWindow" class="talkto-chat-window">
                <!-- Reconnection Warning -->
                <div id="talktoReconnectBanner" class="talkto-reconnect-banner">
                    <i class="fa-solid fa-circle-exclamation me-1"></i> Reconnecting to chat server...
                </div>

                <!-- Window Header -->
                <div class="talkto-chat-header">
                    <div class="talkto-header-branding">
                        <div class="talkto-header-avatar">
                            <img id="talktoHeaderAvatar" src="/images/default-avatar.svg" alt="Avatar" />
                        </div>
                        <div class="talkto-header-info">
                            <h4 id="talktoHeaderTitle">TalkTo Support</h4>
                            <p id="talktoHeaderSubtitle">
                                <span class="talkto-status-dot"></span> Online | We typically reply instantly
                            </p>
                        </div>
                    </div>
                    <div class="talkto-header-actions">
                        <button id="talktoBtnMinimize" class="talkto-header-btn" title="Minimize">
                            <i class="fa-solid fa-minus"></i>
                        </button>
                        <button id="talktoBtnMaximize" class="talkto-header-btn" title="Expand">
                            <i class="fa-solid fa-up-right-and-down-left-from-center"></i>
                        </button>
                        <button id="talktoBtnClose" class="talkto-header-btn" title="Close Chat">
                            <i class="fa-solid fa-xmark"></i>
                        </button>
                    </div>
                </div>

                <!-- Window Body -->
                <div class="talkto-chat-body">
                    <!-- Step 1 & 2: Welcome & Mobile Capture Screen -->
                    <div id="talktoStepMobile" class="talkto-step-panel active">
                        <div class="talkto-welcome-card">
                            <div class="talkto-welcome-icon">
                                <i class="fa-solid fa-headset"></i>
                            </div>
                            <h3>Welcome to Live Chat!</h3>
                            <p>Thank you for contacting us. Please provide your details so our team can assist you.</p>
                        </div>

                        <div class="talkto-form-group">
                            <label class="talkto-form-label">Enter Your Mobile Number</label>
                            <div class="talkto-phone-input-group">
                                <select id="talktoCountryCode" class="talkto-country-select">
                                    <option value="+91" selected>🇮🇳 +91</option>
                                    <option value="+1">🇺🇸 +1</option>
                                    <option value="+44">🇬🇧 +44</option>
                                    <option value="+971">🇦🇪 +971</option>
                                    <option value="+65">🇸🇬 +65</option>
                                </select>
                                <input type="tel" id="talktoMobileInput" class="talkto-phone-input" placeholder="10-digit mobile number" maxlength="10" autocomplete="tel" />
                            </div>
                            <div id="talktoMobileError" class="talkto-error-text">Please enter a valid 10-digit Indian mobile number.</div>
                        </div>

                        <button type="button" id="talktoBtnContinueMobile" class="talkto-btn-primary">
                            <span>Continue</span>
                            <i class="fa-solid fa-arrow-right"></i>
                        </button>
                        <div class="text-center mt-3 text-muted" style="font-size: 11px;">
                            <i class="fa-solid fa-shield-halved me-1 text-success"></i> No OTP or password required. Your number is secure.
                        </div>
                    </div>

                    <!-- Step 3: Service Selection Screen -->
                    <div id="talktoStepService" class="talkto-step-panel">
                        <div class="talkto-welcome-card mb-3">
                            <div class="talkto-welcome-icon" style="background:#EEF2FF; color:#4F46E5;">
                                <i class="fa-solid fa-cubes"></i>
                            </div>
                            <h3 id="talktoWelcomeMobileHeading">Select Your Service</h3>
                            <p>Choose the topic you need assistance with so we can connect you to the right specialist.</p>
                        </div>

                        <div class="talkto-form-group">
                            <label class="talkto-form-label">Select Your Service</label>
                            <select id="talktoServiceSelect" class="talkto-service-select">
                                <option value="">-- Choose a service --</option>
                            </select>
                            <div id="talktoServiceError" class="talkto-error-text">Please select a service from the list.</div>
                        </div>

                        <div id="talktoServiceCardsList" class="talkto-service-cards-preview">
                            <!-- Populated dynamically -->
                        </div>

                        <button type="button" id="talktoBtnStartChat" class="talkto-btn-primary mt-3">
                            <span>Start Chat</span>
                            <i class="fa-solid fa-comments"></i>
                        </button>
                    </div>

                    <!-- Step 4: Live Chat Interface -->
                    <div id="talktoStepChat" class="talkto-chat-interface-panel">
                        <!-- Subheader with assigned agent -->
                        <div class="talkto-agent-subheader">
                            <div class="talkto-agent-sub-left">
                                <div class="talkto-agent-sub-avatar">
                                    <img id="talktoSubAgentAvatar" src="/images/default-avatar.svg" alt="Agent" />
                                </div>
                                <div>
                                    <div id="talktoSubAgentName" class="talkto-agent-sub-name">Support Team</div>
                                    <div id="talktoSubAgentService" class="talkto-agent-sub-service">General Support</div>
                                </div>
                            </div>
                            <span id="talktoSessionStatusBadge" class="talkto-session-badge">Active</span>
                        </div>

                        <!-- Chat Messages List -->
                        <div id="talktoMessagesStream" class="talkto-messages-stream">
                            <div class="talkto-date-divider">
                                <span>Today</span>
                            </div>
                        </div>

                        <!-- Typing Indicator -->
                        <div id="talktoTypingRow" class="talkto-typing-row">
                            <div class="talkto-typing-bubble">
                                <span class="talkto-typing-dot"></span>
                                <span class="talkto-typing-dot"></span>
                                <span class="talkto-typing-dot"></span>
                            </div>
                        </div>

                        <!-- Composer / Input Bar -->
                        <div class="talkto-chat-composer">
                            <!-- Emoji Button & Popover -->
                            <button id="talktoBtnEmoji" type="button" class="talkto-composer-btn" title="Insert Emoji">
                                <i class="fa-regular fa-face-smile"></i>
                            </button>
                            <div id="talktoEmojiPicker" class="talkto-emoji-picker">
                                <span class="talkto-emoji-item">😊</span>
                                <span class="talkto-emoji-item">👍</span>
                                <span class="talkto-emoji-item">🙏</span>
                                <span class="talkto-emoji-item">❤️</span>
                                <span class="talkto-emoji-item">👋</span>
                                <span class="talkto-emoji-item">🎉</span>
                                <span class="talkto-emoji-item">🔥</span>
                                <span class="talkto-emoji-item">😁</span>
                                <span class="talkto-emoji-item">🤔</span>
                                <span class="talkto-emoji-item">🙌</span>
                                <span class="talkto-emoji-item">✨</span>
                                <span class="talkto-emoji-item">🚀</span>
                                <span class="talkto-emoji-item">💯</span>
                                <span class="talkto-emoji-item">👏</span>
                            </div>

                            <!-- File Attachment Button -->
                            <input type="file" id="talktoFileInput" style="display:none;" />
                            <button id="talktoBtnAttach" type="button" class="talkto-composer-btn" title="Attach file or image">
                                <i class="fa-solid fa-paperclip"></i>
                            </button>

                            <!-- Text Input -->
                            <input type="text" id="talktoMessageInput" class="talkto-input-field" placeholder="Type a message..." autocomplete="off" />

                            <!-- Send Button -->
                            <button id="talktoBtnSend" type="button" class="talkto-send-btn" title="Send message">
                                <i class="fa-solid fa-paper-plane"></i>
                            </button>
                        </div>
                    </div>

                    <!-- Step 5: Rating Modal Overlay -->
                    <div id="talktoRatingOverlay" class="talkto-rating-overlay">
                        <div class="talkto-welcome-icon" style="background:#FEF3C7; color:#D97706; margin-bottom:12px;">
                            <i class="fa-solid fa-star"></i>
                        </div>
                        <h4 style="margin:0 0 6px; font-weight:700;">Rate Your Conversation</h4>
                        <p style="font-size:12px; color:#6B7280; text-align:center; margin:0 0 12px;">How was your experience with our support team?</p>
                        
                        <div id="talktoStarGroup" class="talkto-star-rating">
                            <i class="fa-solid fa-star selected" data-rating="1"></i>
                            <i class="fa-solid fa-star selected" data-rating="2"></i>
                            <i class="fa-solid fa-star selected" data-rating="3"></i>
                            <i class="fa-solid fa-star selected" data-rating="4"></i>
                            <i class="fa-solid fa-star selected" data-rating="5"></i>
                        </div>

                        <textarea id="talktoRatingFeedback" class="talkto-rating-feedback" placeholder="Leave an optional comment or feedback..."></textarea>
                        
                        <button type="button" id="talktoBtnSubmitRating" class="talkto-btn-primary" style="margin-top:0;">Submit Rating</button>
                        <button type="button" id="talktoBtnStartNewChat" class="btn btn-link text-muted mt-2" style="font-size:12px; text-decoration:none;">Start a New Chat</button>
                    </div>
                </div>
            </div>

            <!-- Floating Toggle Button -->
            <button id="talktoLauncherBtn" class="talkto-launcher-btn" title="Live Chat Support">
                <i id="talktoLauncherIcon" class="fa-solid fa-comments"></i>
                <span id="talktoUnreadBadge" class="talkto-unread-badge">0</span>
            </button>
        `;

        document.body.appendChild(widgetContainer);

        setupEventListeners();
        setupSignalR();

        // Check if there is an existing session to restore
        if (currentSessionId) {
            restoreExistingSession(currentSessionId);
        } else {
            loadActiveServices();
        }
    }

    // Bind Event Listeners
    function setupEventListeners() {
        const launcherBtn = document.getElementById('talktoLauncherBtn');
        const launcherIcon = document.getElementById('talktoLauncherIcon');
        const chatWindow = document.getElementById('talktoChatWindow');
        const btnMinimize = document.getElementById('talktoBtnMinimize');
        const btnMaximize = document.getElementById('talktoBtnMaximize');
        const btnClose = document.getElementById('talktoBtnClose');
        const btnContinueMobile = document.getElementById('talktoBtnContinueMobile');
        const mobileInput = document.getElementById('talktoMobileInput');
        const serviceSelect = document.getElementById('talktoServiceSelect');
        const btnStartChat = document.getElementById('talktoBtnStartChat');
        const messageInput = document.getElementById('talktoMessageInput');
        const btnSend = document.getElementById('talktoBtnSend');
        const btnEmoji = document.getElementById('talktoBtnEmoji');
        const emojiPicker = document.getElementById('talktoEmojiPicker');
        const btnAttach = document.getElementById('talktoBtnAttach');
        const fileInput = document.getElementById('talktoFileInput');

        // Toggle Open/Close
        launcherBtn.addEventListener('click', () => {
            const isOpen = chatWindow.classList.toggle('open');
            if (isOpen) {
                launcherIcon.className = 'fa-solid fa-chevron-down';
                clearUnreadBadge();
                if (currentSessionId) {
                    scrollToBottom();
                    markCustomerMessagesRead();
                }
            } else {
                launcherIcon.className = 'fa-solid fa-comments';
            }
        });

        // Minimize
        btnMinimize.addEventListener('click', () => {
            chatWindow.classList.remove('open');
            launcherIcon.className = 'fa-solid fa-comments';
        });

        // Maximize/Expand
        btnMaximize.addEventListener('click', () => {
            chatWindow.classList.toggle('maximized');
            const icon = btnMaximize.querySelector('i');
            if (chatWindow.classList.contains('maximized')) {
                icon.className = 'fa-solid fa-down-left-and-up-right-to-center';
            } else {
                icon.className = 'fa-solid fa-up-right-and-down-left-from-center';
            }
        });

        // Close Chat button in header
        btnClose.addEventListener('click', () => {
            if (currentSessionId) {
                if (confirm('Are you sure you want to end this chat session?')) {
                    endCurrentSession();
                }
            } else {
                chatWindow.classList.remove('open');
                launcherIcon.className = 'fa-solid fa-comments';
            }
        });

        // Step 2: Validate Mobile Number
        mobileInput.addEventListener('input', function () {
            this.value = this.value.replace(/\D/g, '').substring(0, 10);
            document.getElementById('talktoMobileError').style.display = 'none';
        });

        mobileInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                btnContinueMobile.click();
            }
        });

        btnContinueMobile.addEventListener('click', async () => {
            const rawNumber = mobileInput.value.trim();
            const countryCode = document.getElementById('talktoCountryCode').value;
            const errorElem = document.getElementById('talktoMobileError');

            // JavaScript validation: 10-digit Indian mobile number
            const isValidIndianMobile = /^[6-9]\d{9}$/.test(rawNumber);
            if (!isValidIndianMobile) {
                errorElem.textContent = 'Please enter a valid 10-digit Indian mobile number starting with 6, 7, 8, or 9.';
                errorElem.style.display = 'block';
                return;
            }

            btnContinueMobile.disabled = true;
            btnContinueMobile.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Validating...';

            try {
                // Server-side validation via ADO.NET
                const res = await fetch('/api/chatwidget/validate-mobile', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        countryCode: countryCode,
                        mobileNumber: rawNumber
                    })
                });

                const data = await res.json();
                if (data.success) {
                    currentVisitorId = data.visitorId;
                    currentMobile = data.mobileNumber;
                    currentCountryCode = data.countryCode;

                    localStorage.setItem('talkto_visitor_id', currentVisitorId);
                    localStorage.setItem('talkto_mobile', currentMobile);
                    localStorage.setItem('talkto_country_code', currentCountryCode);

                    // Proceed directly to Step 3: Service Selection
                    loadActiveServices();
                    showStep('talktoStepService');
                } else {
                    errorElem.textContent = data.message || 'Validation failed. Please check your number.';
                    errorElem.style.display = 'block';
                }
            } catch (err) {
                errorElem.textContent = 'Network error during validation. Please try again.';
                errorElem.style.display = 'block';
            } finally {
                btnContinueMobile.disabled = false;
                btnContinueMobile.innerHTML = '<span>Continue</span> <i class="fa-solid fa-arrow-right"></i>';
            }
        });

        // Step 3: Service Selection & Start Chat
        serviceSelect.addEventListener('change', function () {
            document.getElementById('talktoServiceError').style.display = 'none';
            highlightSelectedServiceCard(this.value);
        });

        btnStartChat.addEventListener('click', async () => {
            const sVal = serviceSelect.value;
            const errorElem = document.getElementById('talktoServiceError');

            if (!sVal || parseInt(sVal, 10) <= 0) {
                errorElem.style.display = 'block';
                return;
            }

            currentServiceId = parseInt(sVal, 10);
            currentServiceName = serviceSelect.options[serviceSelect.selectedIndex].text;

            btnStartChat.disabled = true;
            btnStartChat.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Connecting...';

            try {
                const res = await fetch('/api/chatwidget/start-chat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        visitorId: currentVisitorId,
                        serviceId: currentServiceId
                    })
                });

                const data = await res.json();
                if (data.success) {
                    currentSessionId = data.chatSessionId;
                    localStorage.setItem('talkto_session_id', currentSessionId);
                    localStorage.setItem('talkto_service_id', currentServiceId);
                    localStorage.setItem('talkto_service_name', currentServiceName);

                    renderChatSession(data);
                    showStep('talktoStepChat');

                    // Connect and join SignalR session
                    if (connection) {
                        joinSignalRSession(currentSessionId);
                    }
                } else {
                    alert(data.message || 'Unable to start chat. Please try again.');
                }
            } catch (err) {
                alert('Connection error. Please try again.');
            } finally {
                btnStartChat.disabled = false;
                btnStartChat.innerHTML = '<span>Start Chat</span> <i class="fa-solid fa-comments"></i>';
            }
        });

        // Step 4: Message Sending
        messageInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                sendMessage();
            }
        });

        btnSend.addEventListener('click', () => {
            sendMessage();
        });

        // Live Typing Status Notification
        messageInput.addEventListener('input', () => {
            if (!connection || !currentSessionId) return;

            connection.invoke('SendTyping', currentSessionId, 'Customer', 'Customer', true).catch(() => {});
            clearTimeout(typingTimeout);
            typingTimeout = setTimeout(() => {
                connection.invoke('SendTyping', currentSessionId, 'Customer', 'Customer', false).catch(() => {});
            }, 1800);
        });

        // Emoji Picker Toggle
        btnEmoji.addEventListener('click', (e) => {
            e.stopPropagation();
            emojiPicker.classList.toggle('open');
        });

        document.addEventListener('click', (e) => {
            if (!emojiPicker.contains(e.target) && e.target !== btnEmoji) {
                emojiPicker.classList.remove('open');
            }
        });

        emojiPicker.querySelectorAll('.talkto-emoji-item').forEach(item => {
            item.addEventListener('click', () => {
                messageInput.value += item.textContent;
                messageInput.focus();
                emojiPicker.classList.remove('open');
            });
        });

        // File Attachment
        btnAttach.addEventListener('click', () => {
            fileInput.click();
        });

        fileInput.addEventListener('change', async () => {
            if (!fileInput.files || fileInput.files.length === 0) return;
            const file = fileInput.files[0];

            const formData = new FormData();
            formData.append('file', file);
            formData.append('chatSessionId', currentSessionId);
            formData.append('senderId', currentMobile);
            formData.append('senderType', 'Customer');
            formData.append('senderName', 'Customer');

            btnAttach.disabled = true;
            btnAttach.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i>';

            try {
                const res = await fetch('/api/chatwidget/upload-attachment', {
                    method: 'POST',
                    body: formData
                });
                const data = await res.json();
                if (!data.success) {
                    alert(data.message || 'File upload failed.');
                }
            } catch (err) {
                alert('File upload failed.');
            } finally {
                btnAttach.disabled = false;
                btnAttach.innerHTML = '<i class="fa-solid fa-paperclip"></i>';
                fileInput.value = '';
            }
        });

        // Rating Stars Interaction
        const stars = document.querySelectorAll('#talktoStarGroup i');
        stars.forEach(star => {
            star.addEventListener('click', function () {
                selectedRating = parseInt(this.getAttribute('data-rating'), 10);
                updateStarDisplay(selectedRating);
            });
        });

        document.getElementById('talktoBtnSubmitRating').addEventListener('click', async () => {
            const feedback = document.getElementById('talktoRatingFeedback').value.trim();
            try {
                await fetch('/api/chatwidget/rate-chat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        chatSessionId: currentSessionId,
                        rating: selectedRating,
                        feedback: feedback
                    })
                });
                alert('Thank you for your rating!');
                clearSessionStorage();
                showStep('talktoStepMobile');
                document.getElementById('talktoRatingOverlay').classList.remove('open');
            } catch (e) {
                clearSessionStorage();
                showStep('talktoStepMobile');
                document.getElementById('talktoRatingOverlay').classList.remove('open');
            }
        });

        document.getElementById('talktoBtnStartNewChat').addEventListener('click', () => {
            clearSessionStorage();
            showStep('talktoStepMobile');
            document.getElementById('talktoRatingOverlay').classList.remove('open');
        });
    }

    function updateStarDisplay(rating) {
        const stars = document.querySelectorAll('#talktoStarGroup i');
        stars.forEach(s => {
            const r = parseInt(s.getAttribute('data-rating'), 10);
            if (r <= rating) {
                s.className = 'fa-solid fa-star selected';
            } else {
                s.className = 'fa-solid fa-star text-muted';
            }
        });
    }

    function showStep(stepId) {
        document.querySelectorAll('.talkto-step-panel, .talkto-chat-interface-panel').forEach(p => {
            p.classList.remove('active');
        });
        const target = document.getElementById(stepId);
        if (target) {
            target.classList.add('active');
        }
    }

    // Load Services dynamically from Database
    async function loadActiveServices() {
        try {
            const res = await fetch('/api/chatwidget/services');
            const data = await res.json();
            if (data.success && data.data) {
                const select = document.getElementById('talktoServiceSelect');
                const cardsContainer = document.getElementById('talktoServiceCardsList');

                select.innerHTML = '<option value="">-- Choose a service --</option>';
                cardsContainer.innerHTML = '';

                const serviceIcons = {
                    'Technical Support': 'fa-screwdriver-wrench',
                    'Website Development': 'fa-code',
                    'Software Development': 'fa-laptop-code',
                    'Payment Related Query': 'fa-credit-card',
                    'Product Enquiry': 'fa-box-open',
                    'Sales & Marketing': 'fa-bullhorn',
                    'Account Related Query': 'fa-user-gear',
                    'General Enquiry': 'fa-circle-question',
                    'Other': 'fa-comments'
                };

                data.data.forEach(s => {
                    // Option
                    const opt = document.createElement('option');
                    opt.value = s.serviceId;
                    opt.textContent = s.serviceName;
                    select.appendChild(opt);

                    // Visual card
                    const iconClass = serviceIcons[s.serviceName] || 'fa-tag';
                    const card = document.createElement('div');
                    card.className = 'talkto-service-card-item';
                    card.setAttribute('data-id', s.serviceId);
                    card.innerHTML = `
                        <i class="fa-solid ${iconClass}"></i>
                        <div>
                            <div class="service-name">${escapeHtml(s.serviceName)}</div>
                            ${s.description ? `<div style="font-size:11px; color:#6B7280;">${escapeHtml(s.description)}</div>` : ''}
                        </div>
                    `;
                    card.addEventListener('click', () => {
                        select.value = s.serviceId;
                        highlightSelectedServiceCard(s.serviceId);
                        document.getElementById('talktoServiceError').style.display = 'none';
                    });
                    cardsContainer.appendChild(card);
                });
            }
        } catch (e) {
            console.error('Failed to load services:', e);
        }
    }

    function highlightSelectedServiceCard(serviceId) {
        document.querySelectorAll('.talkto-service-card-item').forEach(c => {
            if (c.getAttribute('data-id') == serviceId) {
                c.classList.add('selected');
            } else {
                c.classList.remove('selected');
            }
        });
    }

    // Render Chat Session on Start
    function renderChatSession(sessionData) {
        document.getElementById('talktoHeaderTitle').textContent = sessionData.agentName || 'TalkTo Support';
        document.getElementById('talktoSubAgentName').textContent = sessionData.agentName || 'Support Specialist';
        document.getElementById('talktoSubAgentService').textContent = sessionData.serviceName || 'General';
        document.getElementById('talktoSessionStatusBadge').textContent = sessionData.sessionStatus || 'Active';

        if (sessionData.agentProfileImage) {
            document.getElementById('talktoHeaderAvatar').src = sessionData.agentProfileImage;
            document.getElementById('talktoSubAgentAvatar').src = sessionData.agentProfileImage;
        }

        const messagesContainer = document.getElementById('talktoMessagesStream');
        messagesContainer.innerHTML = '<div class="talkto-date-divider"><span>Today</span></div>';

        // Welcome or Pending initial message
        if (sessionData.welcomeMessage) {
            appendMessageBubble({
                senderType: (sessionData.sessionStatus === 'Pending') ? 'System' : 'Agent',
                senderName: sessionData.agentName || 'Support',
                messageText: sessionData.welcomeMessage,
                sentDate: new Date(),
                isRead: true
            });
        }
    }

    // Restore Existing Session from LocalStorage
    async function restoreExistingSession(sessionId) {
        try {
            const res = await fetch(`/api/chatwidget/session/${encodeURIComponent(sessionId)}`);
            const data = await res.json();
            if (data.success && data.session) {
                const s = data.session;
                currentVisitorId = s.visitorId;
                currentMobile = s.mobileNumber;
                currentCountryCode = s.countryCode;
                currentServiceId = s.serviceId;
                currentServiceName = s.serviceName;

                renderChatSession({
                    agentName: s.agentName,
                    agentProfileImage: s.agentProfileImage,
                    serviceName: s.serviceName,
                    sessionStatus: s.sessionStatus
                });

                // Clear & load message history
                const messagesContainer = document.getElementById('talktoMessagesStream');
                messagesContainer.innerHTML = '<div class="talkto-date-divider"><span>Today</span></div>';

                if (data.messages && data.messages.length > 0) {
                    data.messages.forEach(msg => {
                        appendMessageBubble(msg);
                    });
                }

                showStep('talktoStepChat');
                scrollToBottom();

                if (s.sessionStatus === 'Completed' || s.sessionStatus === 'Closed') {
                    // Show rating overlay if not yet rated
                    if (!s.rating) {
                        document.getElementById('talktoRatingOverlay').classList.add('open');
                    }
                }
            } else {
                clearSessionStorage();
                loadActiveServices();
                showStep('talktoStepMobile');
            }
        } catch (e) {
            clearSessionStorage();
            loadActiveServices();
            showStep('talktoStepMobile');
        }
    }

    // SignalR Setup
    function setupSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/chatHub')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.onreconnecting(() => {
            document.getElementById('talktoReconnectBanner').style.display = 'block';
        });

        connection.onreconnected(() => {
            document.getElementById('talktoReconnectBanner').style.display = 'none';
            if (currentSessionId) {
                joinSignalRSession(currentSessionId);
            }
        });

        connection.onclose(() => {
            document.getElementById('talktoReconnectBanner').style.display = 'block';
        });

        // Hub Event: Receive Message
        connection.on('ReceiveMessage', (msg) => {
            if (msg.chatSessionId === currentSessionId) {
                appendMessageBubble(msg);
                scrollToBottom();

                if (msg.senderType === 'Agent' || msg.senderType === 'System') {
                    playNotificationSound();
                    const chatWindow = document.getElementById('talktoChatWindow');
                    if (!chatWindow.classList.contains('open')) {
                        incrementUnreadBadge();
                    } else {
                        markCustomerMessagesRead();
                    }
                }
            }
        });

        // Hub Event: User Typing
        connection.on('UserTyping', (data) => {
            if (data.chatSessionId === currentSessionId && data.senderType === 'Agent') {
                const typingRow = document.getElementById('talktoTypingRow');
                if (data.isTyping) {
                    typingRow.style.display = 'flex';
                    scrollToBottom();
                } else {
                    typingRow.style.display = 'none';
                }
            }
        });

        // Hub Event: Messages Read
        connection.on('MessagesRead', (data) => {
            if (data.chatSessionId === currentSessionId && data.readerType === 'Agent') {
                document.querySelectorAll('.talkto-msg-row.customer .talkto-ticks').forEach(el => {
                    el.className = 'talkto-ticks read';
                    el.innerHTML = '✓✓';
                });
            }
        });

        // Hub Event: Agent Assigned
        connection.on('AgentAssigned', (data) => {
            if (data.chatSessionId === currentSessionId) {
                document.getElementById('talktoHeaderTitle').textContent = data.agentName;
                document.getElementById('talktoSubAgentName').textContent = data.agentName;
                document.getElementById('talktoSessionStatusBadge').textContent = 'Active';
            }
        });

        // Hub Event: Chat Transferred
        connection.on('ChatTransferred', (data) => {
            if (data.chatSessionId === currentSessionId) {
                document.getElementById('talktoHeaderTitle').textContent = data.newAgentName;
                document.getElementById('talktoSubAgentName').textContent = data.newAgentName;
            }
        });

        // Hub Event: Chat Closed
        connection.on('ChatClosed', (data) => {
            if (data.chatSessionId === currentSessionId) {
                document.getElementById('talktoSessionStatusBadge').textContent = 'Completed';
                document.getElementById('talktoRatingOverlay').classList.add('open');
            }
        });

        // Start Connection
        connection.start().then(() => {
            isConnected = true;
            if (currentSessionId) {
                joinSignalRSession(currentSessionId);
            }
        }).catch(err => {
            console.error('SignalR connection error:', err);
        });
    }

    function joinSignalRSession(sessionId) {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;
        connection.invoke('JoinSession', sessionId, 'Customer', currentMobile, 'Customer')
            .catch(err => console.error('JoinSession error:', err));
    }

    // Send Message
    async function sendMessage() {
        const input = document.getElementById('talktoMessageInput');
        const text = input.value.trim();
        if (!text || !currentSessionId) return;

        input.value = '';
        input.focus();

        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            try {
                await connection.invoke(
                    'SendMessage',
                    currentSessionId,
                    currentMobile || 'Customer',
                    'Customer',
                    'Customer',
                    text,
                    'Text',
                    null,
                    null,
                    null
                );
            } catch (err) {
                console.error('SendMessage error:', err);
            }
        }
    }

    function appendMessageBubble(msg) {
        const stream = document.getElementById('talktoMessagesStream');
        const row = document.createElement('div');
        const isCustomer = msg.senderType === 'Customer';
        const isSystem = msg.senderType === 'System';

        row.className = `talkto-msg-row ${isCustomer ? 'customer' : (isSystem ? 'system' : 'agent')}`;

        const timeStr = formatTime(msg.sentDate);
        let contentHtml = '';

        if (msg.messageType === 'Image' && msg.filePath) {
            contentHtml += `<img src="${msg.filePath}" alt="Image" class="talkto-bubble-image" onclick="window.open('${msg.filePath}', '_blank')" />`;
        } else if (msg.messageType === 'File' && msg.filePath) {
            contentHtml += `
                <a href="${msg.filePath}" target="_blank" download class="talkto-bubble-file">
                    <i class="fa-solid fa-file-arrow-down"></i>
                    <div class="talkto-bubble-file-info">
                        <div class="talkto-bubble-file-name">${escapeHtml(msg.fileName || 'Attachment')}</div>
                        <div style="font-size:10px; color:#6B7280;">Click to download</div>
                    </div>
                </a>
            `;
        }

        contentHtml += `<div>${escapeHtml(msg.messageText || '')}</div>`;

        if (!isSystem) {
            const checkmarks = isCustomer ? `<span class="talkto-ticks ${msg.isRead ? 'read' : ''}">✓✓</span>` : '';
            contentHtml += `
                <div class="talkto-bubble-meta">
                    <span>${timeStr}</span>
                    ${checkmarks}
                </div>
            `;
        }

        row.innerHTML = `<div class="talkto-bubble">${contentHtml}</div>`;
        stream.appendChild(row);
    }

    function markCustomerMessagesRead() {
        if (!connection || !currentSessionId) return;
        connection.invoke('MarkAsRead', currentSessionId, 'Customer').catch(() => {});
    }

    function endCurrentSession() {
        if (!connection || !currentSessionId) return;
        connection.invoke('CloseChat', currentSessionId, 'Customer').then(() => {
            document.getElementById('talktoRatingOverlay').classList.add('open');
        }).catch(err => {
            console.error('Error closing chat:', err);
        });
    }

    function clearSessionStorage() {
        currentSessionId = null;
        localStorage.removeItem('talkto_session_id');
        localStorage.removeItem('talkto_service_id');
        localStorage.removeItem('talkto_service_name');
    }

    function incrementUnreadBadge() {
        unreadCount++;
        const badge = document.getElementById('talktoUnreadBadge');
        badge.textContent = unreadCount > 9 ? '9+' : unreadCount;
        badge.style.display = 'flex';
    }

    function clearUnreadBadge() {
        unreadCount = 0;
        const badge = document.getElementById('talktoUnreadBadge');
        badge.style.display = 'none';
        badge.textContent = '0';
    }

    function scrollToBottom() {
        const stream = document.getElementById('talktoMessagesStream');
        if (stream) {
            stream.scrollTop = stream.scrollHeight;
        }
    }

    function formatTime(dateVal) {
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

    // Auto boot on DOM Ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initWidget);
    } else {
        initWidget();
    }
})();
