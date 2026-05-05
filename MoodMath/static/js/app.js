const menuItems = document.querySelectorAll('.menu-item');
const sections = document.querySelectorAll('.section');
const btnGoToSettings = document.getElementById('btnGoToSettings');

// Settings Button (Top Right)
if (btnGoToSettings) {

    btnGoToSettings.addEventListener('click', () => {
        menuItems.forEach(i => i.classList.remove('active'));
        sections.forEach(s => s.classList.remove('active'));
        document.getElementById('page5').classList.add('active');
    });
}

menuItems.forEach(item => {
    item.addEventListener('click', () => {
        menuItems.forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        
        sections.forEach(s => s.classList.remove('active'));
        const target = item.dataset.target;
        document.getElementById(target).classList.add('active');

        // Sidebar & Chat Mode Toggle
        if (target === 'page4') {
            document.getElementById('chatSidebar').classList.add('visible');
            document.body.classList.add('sidebar-active');
            document.body.classList.add('chat-mode');
            if (chats.length === 0) createNewChat();
            else renderChatList();
        } else {
            document.getElementById('chatSidebar').classList.remove('visible');
            document.body.classList.remove('sidebar-active');
            document.body.classList.remove('chat-mode');
        }
    });
});


function showAlert(parent, type, message) {
    const el = document.getElementById(parent);
    el.className = `alert visible ${type}`;
    el.innerText = message;
}
function hideAlert(parent) {
    const el = document.getElementById(parent);
    el.className = 'alert';
}

const alertP2 = document.getElementById('alertP2');
const alertP3 = document.getElementById('alertP3');
const alertP5 = document.getElementById('alertP5');

// Settings Elements
const inputApiKey = document.getElementById('inputApiKey');
const inputWaitTime = document.getElementById('inputWaitTime');
const btnSaveSettings = document.getElementById('btnSaveSettings');

// Model Selection & Balloon elements (Multi-instance)
const btnToggles = document.querySelectorAll('.btnToggleModelList');
const balloonCloseBtns = document.querySelectorAll('.close-balloon');
const modelOptions = document.querySelectorAll('.model-option');


function setLoading(btnId, isLoading) {


    const btn = document.getElementById(btnId);
    if(isLoading) {
        btn.classList.add('loading');
        btn.disabled = true;
    } else {
        btn.classList.remove('loading');
        btn.disabled = false;
    }
}

// Page 1: Pipeline
let currentChart = null;

const dropArea1 = document.getElementById('dropArea');
const uploadRaw = document.getElementById('uploadFileRaw');
const fileInfo1 = document.getElementById('fileInfo1');

dropArea1.addEventListener('click', () => uploadRaw.click());
uploadRaw.addEventListener('change', () => {
    if(uploadRaw.files.length > 0) fileInfo1.innerText = "Seçilen dosya: " + uploadRaw.files[0].name;
});

document.getElementById('btnClean').addEventListener('click', async () => {
    if(!uploadRaw.files.length) return showAlert('alertP1', 'error', 'Lütfen bir veri dosyası seçin!');
    
    setLoading('btnClean', true);
    hideAlert('alertP1');
    const formData = new FormData();
    formData.append("file", uploadRaw.files[0]);

    try {
        const r = await fetch('/api/upload-clean', { method: 'POST', body: formData });
        const data = await r.json();
        
        if(!r.ok) throw new Error(data.error);
        
        showAlert('alertP1', 'success', data.message);
        document.getElementById('dashboard1').style.display = 'block';
        
        document.getElementById('valTotal').innerText = data.total;
        document.getElementById('valPos').innerText = data.pos;
        document.getElementById('valNeg').innerText = data.neg;

        // Render Chart.js
        if(currentChart) currentChart.destroy();
        const ctx = document.getElementById('chartSentiment').getContext('2d');
        currentChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Pozitif', 'Negatif'],
                datasets: [{
                    data: [data.pos, data.neg],
                    backgroundColor: ['rgba(16, 185, 129, 0.8)', 'rgba(239, 68, 68, 0.8)'],
                    borderWidth: 0,
                    hoverOffset: 10
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { labels: { color: '#f8fafc', font: {size: 14} } }
                }
            }
        });

    } catch(err) {
        showAlert('alertP1', 'error', err.message);
    } finally {
        setLoading('btnClean', false);
    }
});

// Page 2: Train
const sliderT = document.getElementById('sliderTest');
const testV = document.getElementById('testVal');
sliderT.addEventListener('input', () => testV.innerText = sliderT.value);

document.getElementById('btnTrain').addEventListener('click', async () => {
    setLoading('btnTrain', true);
    hideAlert('alertP2');
    try {
        const payload = {
            test_size: parseFloat(sliderT.value)
        };
        const r = await fetch('/api/train', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(payload)
        });
        const data = await r.json();

        if(!r.ok) throw new Error(data.error);

        showAlert('alertP2', 'success', data.message);
        document.getElementById('dashboard2').style.display = 'block';

        const rep = data.metrics.report;
        document.getElementById('accVal').innerText = `% ${(data.metrics.accuracy * 100).toFixed(2)}`;

        const key1 = rep['1'] || rep[1];
        const key0 = rep['0'] || rep[0];

        const f_html = (k) => k ? `
            <p style="margin-bottom:0.5rem;"><strong>Precision:</strong> % ${(k['precision']*100).toFixed(1)}</p>
            <p style="margin-bottom:0.5rem;"><strong>Recall:</strong> % ${(k['recall']*100).toFixed(1)}</p>
            <p><strong>F1-Score:</strong> % ${(k['f1-score']*100).toFixed(1)}</p>
        ` : `<p>Veri bulunamadı.</p>`;

        document.getElementById('posMetrics').innerHTML = f_html(key1);
        document.getElementById('negMetrics').innerHTML = f_html(key0);

    } catch(err) {
        showAlert('alertP2', 'error', err.message);
    } finally {
        setLoading('btnTrain', false);
    }
});

// Page 3: Predict Arena
document.getElementById('btnPredict').addEventListener('click', async () => {
    const text = document.getElementById('txtPredict').value;
    if(text.length < 3) return showAlert('alertP3', 'error', 'Çok kısa metin!');

    setLoading('btnPredict', true);
    hideAlert('alertP3');
    try {
        const r = await fetch('/api/predict-arena', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({ text })
        });
        const data = await r.json();
        if(!r.ok) throw new Error(data.error);

        document.getElementById('dashboard3').style.display = 'block';

        // --- MOODMATH UI GÜNCELLEMESİ ---
        const predBox = document.getElementById('predBox');
        const moodPositivity = data.moodmath.positivity_score;

        if (moodPositivity >= 50) {
            predBox.style.borderColor = 'var(--success)';
            predBox.style.background = 'rgba(16, 185, 129, 0.05)';
            document.getElementById('predScore').style.color = 'var(--success)';
            document.getElementById('predScore').innerText = `%${moodPositivity.toFixed(1)} Pozitif`;
        } else {
            predBox.style.borderColor = 'var(--error)';
            predBox.style.background = 'rgba(239, 68, 68, 0.05)';
            document.getElementById('predScore').style.color = 'var(--error)';
            const moodNegativity = 100 - moodPositivity;
            document.getElementById('predScore').innerText = `%${moodNegativity.toFixed(1)} Negatif`;
        }

        const wordsDiv = document.getElementById('predWords');
        wordsDiv.innerHTML = "";
        if(data.moodmath.words_found.length === 0) {
            wordsDiv.innerHTML = `<div class="alert visible" style="background: rgba(255,255,255,0.05); color:#94a3b8; border:1px solid rgba(255,255,255,0.1)">Model eşleşen bir kelime bulamadı.</div>`;
        } else {
            data.moodmath.words_found.forEach(w => {
                const sp = document.createElement('span');
                sp.className = 'badge pos';
                sp.style.background = 'linear-gradient(135deg, rgba(59, 130, 246, 0.4), rgba(37, 99, 235, 0.4))';
                sp.style.color = '#bfdbfe';
                sp.style.border = '1px solid rgba(59, 130, 246, 0.5)';
                sp.innerText = w;
                wordsDiv.appendChild(sp);
            });
        }

        // --- GEMINI UI GÜNCELLEMESİ ---
        let rawGeminiText = data.gemini.result;

        // Düzenli ifade ile POZİTİFLİK oranını bul
        const scoreMatch = rawGeminiText.match(/POZİTİFLİK:\s*%?(\d+)/i);
        if(scoreMatch) {
            document.getElementById('geminiScoreBox').style.display = 'block';
            let geminiPositivity = parseFloat(scoreMatch[1]);
            const gemScoreEl = document.getElementById('geminiScore');

            if (geminiPositivity >= 50) {
                gemScoreEl.style.color = 'var(--success)';
                gemScoreEl.innerText = `%${geminiPositivity.toFixed(0)} Pozitif`;
            } else {
                gemScoreEl.style.color = 'var(--error)';
                let geminiNegativity = 100 - geminiPositivity;
                gemScoreEl.innerText = `%${geminiNegativity.toFixed(0)} Negatif`;
            }
            // Arayüze basarken ham metinden bu satırı silelim
            rawGeminiText = rawGeminiText.replace(/POZİTİFLİK:.*?\n?/i, "");
        }

        let geminiHtml = rawGeminiText;
        geminiHtml = geminiHtml.replace(/KARAR:\s*POZİTİF/gi, '<strong style="color: var(--success); font-size: 1.2rem;">KARAR: POZİTİF</strong><br>');
        geminiHtml = geminiHtml.replace(/KARAR:\s*NEGATİF/gi, '<strong style="color: var(--error); font-size: 1.2rem;">KARAR: NEGATİF</strong><br>');
        geminiHtml = geminiHtml.replace(/SEBEP:/gi, '<br><strong style="color: var(--primary);">SEBEP:</strong>');

        document.getElementById('geminiResult').innerHTML = geminiHtml;

    } catch(err) {
        showAlert('alertP3', 'error', err.message);
    } finally {
        setLoading('btnPredict', false);
    }
});

// --- Page 5: Settings ---
async function fetchSettings() {
    try {
        const response = await fetch('/api/settings');
        const data = await response.json();
        if (inputApiKey) inputApiKey.value = data.gemini_api_key;
        if (inputWaitTime) inputWaitTime.value = data.scraper_wait_time;
    } catch (err) {
        console.error("Ayarlar yüklenemedi:", err);
    }
}

if (btnSaveSettings) {
    btnSaveSettings.addEventListener('click', async () => {
        const apiKey = inputApiKey.value.trim();
        const waitTime = parseFloat(inputWaitTime.value);

        if (!apiKey) {
            showAlert('alertP5', 'error', 'API Key boş olamaz!');
            return;
        }

        setLoading('btnSaveSettings', true);
        hideAlert('alertP5');
        try {
            const response = await fetch('/api/settings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    gemini_api_key: apiKey,
                    scraper_wait_time: waitTime
                })
            });
            const data = await response.json();
            if (response.ok) {
                showAlert('alertP5', 'success', data.message);
            } else {
                throw new Error(data.error || "Bilinmeyen hata");
            }
        } catch (err) {
            showAlert('alertP5', 'error', "Hata: " + err.message);
        } finally {
            setLoading('btnSaveSettings', false);
        }
    });
}

// Uygulama yüklenince ayarları çek
fetchSettings();


// --- MODEL SELECTION & BALLOON LOGIC (Multi-instance) ---

// Dropdown aç/kapat
btnToggles.forEach(btn => {
    btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const dropdown = btn.nextElementSibling;
        dropdown.classList.toggle('active');
    });
});

// Balonları kapat
balloonCloseBtns.forEach(btn => {
    btn.addEventListener('click', () => {
        btn.closest('.metrics-balloon').classList.remove('visible');
    });
});

// Model seçimi ve metrikleri göster
modelOptions.forEach(opt => {
    opt.addEventListener('click', async () => {
        const wrapper = opt.closest('.input-area');
        const balloon = wrapper.querySelector('.metrics-balloon');
        const dropdown = opt.closest('.model-dropdown');
        
        // UI güncelle
        dropdown.querySelectorAll('.model-option').forEach(o => o.classList.remove('active'));
        opt.classList.add('active');
        dropdown.classList.remove('active');

        // Metrikleri çek ve balonu göster
        try {
            const r = await fetch('/api/model-metrics');
            if (!r.ok) throw new Error("Metrikler alınamadı");
            const data = await r.json();

            balloon.querySelector('.balloonAcc').innerText = `%${(data.accuracy * 100).toFixed(1)}`;
            const macro = data.report['macro avg'];
            balloon.querySelector('.balloonPrec').innerText = `%${(macro.precision * 100).toFixed(1)}`;
            balloon.querySelector('.balloonRec').innerText = `%${(macro.recall * 100).toFixed(1)}`;
            balloon.querySelector('.balloonF1').innerText = `%${(macro['f1-score'] * 100).toFixed(1)}`;

            balloon.classList.add('visible');
        } catch (err) {
            console.error(err);
        }
    });
});

// Dışarı tıklayınca kapatma
document.addEventListener('click', (e) => {
    // Dropdownlar (Model Seçimi vb.)
    document.querySelectorAll('.model-dropdown.active').forEach(d => {
        if (!d.contains(e.target) && !d.previousElementSibling.contains(e.target)) {
            d.classList.remove('active');
        }
    });

    // Balonlar
    document.querySelectorAll('.metrics-balloon.visible').forEach(b => {
        if (!b.contains(e.target)) {
            b.classList.remove('visible');
        }
    });

    // Chat Action Menu
    if (chatActionMenu && !chatActionMenu.contains(e.target) && !e.target.closest('.btn-menu')) {
        chatActionMenu.classList.remove('visible');
    }

    // Confirmation Modal
    if (confirmModal && e.target === confirmModal) {
        closeConfirmModal();
    }
});

// --- Page 4: Chat Interface (Multiple Chats Logic) ---

const chatContainer = document.getElementById('chatContainer');
const messagesArea = document.getElementById('messagesArea');
const chatInput = document.getElementById('chatInput');
const btnSend = document.getElementById('btnSend');
const chatList = document.getElementById('chatList');
const btnNewChat = document.getElementById('btnNewChat');
const currentChatTitle = document.getElementById('currentChatTitle');

let chats = JSON.parse(localStorage.getItem('moodmath_chats')) || [];
let currentChatId = localStorage.getItem('moodmath_active_chat_id') || null;
let isFirstMessage = true;

function saveChats() {
    localStorage.setItem('moodmath_chats', JSON.stringify(chats));
    localStorage.setItem('moodmath_active_chat_id', currentChatId);
}

function renderChatList() {
    chatList.innerHTML = '';
    chats.forEach(chat => {
        const item = document.createElement('div');
        item.className = `chat-item ${chat.id === currentChatId ? 'active' : ''}`;
        item.setAttribute('data-id', chat.id);
        item.onclick = (e) => {
            // Don't switch if clicking the menu button
            if (e.target.closest('.btn-menu')) return;
            switchChat(chat.id);
        };

        item.innerHTML = `
            <div class="chat-name" title="${chat.name}">${chat.name}</div>
            <div class="chat-actions">
                <button class="btn-menu" onclick="event.stopPropagation(); toggleActionMenu(event, '${chat.id}')">
                    <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"></path></svg>
                </button>
            </div>
        `;
        chatList.appendChild(item);
    });
}

function createNewChat() {
    const id = Date.now().toString();
    const newChat = {
        id: id,
        name: `Sohbet ${chats.length + 1}`,
        messages: []
    };
    chats.unshift(newChat); // Add to beginning
    currentChatId = id;
    saveChats();
    switchChat(id);
    renderChatList();
}

function switchChat(id) {
    currentChatId = id;
    const chat = chats.find(c => c.id === id);
    if (!chat) return;

    currentChatTitle.innerText = chat.name;
    messagesArea.innerHTML = '';
    
    if (chat.messages.length > 0) {
        chatContainer.classList.add('active');
        isFirstMessage = false;
        chat.messages.forEach(msg => {
            const msgDiv = document.createElement('div');
            msgDiv.classList.add('message', msg.sender);
            msgDiv.innerHTML = msg.text;
            messagesArea.appendChild(msgDiv);
        });
        messagesArea.scrollTop = messagesArea.scrollHeight;
    } else {
        chatContainer.classList.remove('active');
        isFirstMessage = true;
    }
    
    saveChats();
    renderChatList();
}

function startRename(id) {
    const chat = chats.find(c => c.id === id);
    const item = document.querySelector(`.chat-item[data-id="${id}"]`);
    if (!item) return;

    const nameEl = item.querySelector('.chat-name');
    const oldName = chat.name;

    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'chat-name-input';
    input.value = oldName;
    
    input.onblur = () => finishRename(id, input.value);
    input.onkeypress = (e) => {
        if (e.key === 'Enter') finishRename(id, input.value);
    };

    nameEl.innerHTML = '';
    nameEl.appendChild(input);
    input.focus();
    input.select();
}

function finishRename(id, newName) {
    const chat = chats.find(c => c.id === id);
    if (chat && newName.trim()) {
        chat.name = newName.trim();
        if (id === currentChatId) currentChatTitle.innerText = chat.name;
        saveChats();
        renderChatList();
    } else {
        renderChatList();
    }
}

let activeChatIdInMenu = null;
const chatActionMenu = document.getElementById('chatActionMenu');
const confirmModal = document.getElementById('confirmModal');
const btnConfirmDelete = document.getElementById('btnConfirmDelete');
const btnConfirmCancel = document.getElementById('btnConfirmCancel');

function toggleActionMenu(e, id) {
    activeChatIdInMenu = id;
    chatActionMenu.classList.add('visible');
    
    // Position menu near the button
    const rect = e.target.closest('.btn-menu').getBoundingClientRect();
    chatActionMenu.style.top = `${rect.bottom + 5}px`;
    chatActionMenu.style.left = `${rect.left - 130}px`;
}

function handleRenameClick() {
    chatActionMenu.classList.remove('visible');
    if (activeChatIdInMenu) startRename(activeChatIdInMenu);
}

function handleDeleteClick() {
    chatActionMenu.classList.remove('visible');
    if (activeChatIdInMenu) showConfirmModal(activeChatIdInMenu);
}

function showConfirmModal(id) {
    activeChatIdInMenu = id;
    confirmModal.classList.add('visible');
}

function closeConfirmModal() {
    confirmModal.classList.remove('visible');
    activeChatIdInMenu = null;
}

function deleteChat(id) {
    chats = chats.filter(c => c.id !== id);
    if (currentChatId === id) {
        currentChatId = chats.length > 0 ? chats[0].id : null;
    }
    
    if (!currentChatId) {
        createNewChat();
    } else {
        switchChat(currentChatId);
    }
    
    saveChats();
    renderChatList();
    closeConfirmModal();
}

btnConfirmDelete.onclick = () => {
    if (activeChatIdInMenu) deleteChat(activeChatIdInMenu);
};

btnConfirmCancel.onclick = closeConfirmModal;

if (btnNewChat) {
    btnNewChat.onclick = createNewChat;
}

// Attach to window for onclick handlers
window.toggleActionMenu = toggleActionMenu;
window.handleRenameClick = handleRenameClick;
window.handleDeleteClick = handleDeleteClick;
window.switchChat = switchChat;


// Mesaj ekleme fonksiyonu (Sadece UI değil, veriye de ekler)
function addMessage(text, sender) {
    if (!messagesArea) return;
    
    const chat = chats.find(c => c.id === currentChatId);
    if (chat) {
        chat.messages.push({ text, sender });
        saveChats();
    }

    const msgDiv = document.createElement('div');
    msgDiv.classList.add('message', sender);
    msgDiv.innerHTML = text; // innerHTML for bot replies that might have formatting
    messagesArea.appendChild(msgDiv);
    
    setTimeout(() => {
        messagesArea.scrollTop = messagesArea.scrollHeight;
    }, 50);
}

// Gönder eylemi
async function handleSend() {
    if (!chatInput) return;
    const text = chatInput.value.trim();
    if (!text) return;

    // Input'u temizle
    chatInput.value = '';

    // İlk mesaj gönderildiğinde ortadaki input alanını alta kaydır (animasyon)
    if (isFirstMessage) {
        chatContainer.classList.add('active');
        isFirstMessage = false;
    }

    // Kullanıcı mesajını ekle
    addMessage(text, 'user');

    // Bot için bir mesaj balonu oluştur ve referansını al
    const msgDiv = document.createElement('div');
    msgDiv.classList.add('message', 'bot');
    msgDiv.innerHTML = '<span class="spinner" style="display:inline-block; border-color:var(--primary); border-top-color:transparent; width:15px; height:15px; margin-right:8px; vertical-align:middle;"></span> Sistem başlatılıyor...';
    messagesArea.appendChild(msgDiv);
    messagesArea.scrollTop = messagesArea.scrollHeight;
    
    // Mesajı veriye de ekleyelim ama bot cevabı bittiğinde asıl cevabı kaydedeceğiz
    // Geçici olarak "Sistem başlatılıyor..." demiyoruz, placeholder kalsın.

    // API çağrısı yap (Streaming)
    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message: text, chat_id: currentChatId })
        });
        
        if (!response.body) throw new Error("Tarayıcı streaming desteklemiyor.");

        const reader = response.body.getReader();
        const decoder = new TextDecoder("utf-8");
        
        while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            
            const chunk = decoder.decode(value, { stream: true });
            const lines = chunk.split('\n').filter(line => line.trim() !== '');
            
            for (const line of lines) {
                try {
                    const data = JSON.parse(line);
                    
                    if (data.status === "progress") {
                        msgDiv.innerHTML = `<span class="spinner" style="display:inline-block; border-color:var(--primary); border-top-color:transparent; width:15px; height:15px; margin-right:8px; vertical-align:middle;"></span> ${data.message}`;
                    } 
                    else if (data.status === "done") {
                        msgDiv.innerHTML = data.reply;
                        // Bot cevabını kaydet
                        const chat = chats.find(c => c.id === currentChatId);
                        if (chat) {
                            chat.messages.push({ text: data.reply, sender: 'bot' });
                            saveChats();
                        }
                    }
                    else if (data.status === "error") {
                        msgDiv.innerHTML = `<span style="color:var(--error);">Hata: ${data.message}</span>`;
                    }
                    messagesArea.scrollTop = messagesArea.scrollHeight;
                } catch (e) {
                    console.error("JSON parse hatası:", e, "Gelen parça:", line);
                }
            }
        }
    } catch (err) {
        msgDiv.innerHTML = `<span style="color:var(--error);">Sistemsel Hata: ${err.message}</span>`;
    }
}

// Butona tıklandığında gönder
if (btnSend) {
    btnSend.addEventListener('click', handleSend);
}

// Enter tuşuna basıldığında gönder
if (chatInput) {
    chatInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            handleSend();
        }
    });
}

// --- Initialization on Load ---
document.addEventListener('DOMContentLoaded', () => {
    fetchSettings();
    
    const activeSection = document.querySelector('.section.active');
    if (activeSection && activeSection.id === 'page4') {
        document.getElementById('chatSidebar').classList.add('visible');
        document.body.classList.add('sidebar-active');
        document.body.classList.add('chat-mode');
        if (chats.length === 0) createNewChat();
        else renderChatList();
        
        if (currentChatId) switchChat(currentChatId);
        else if (chats.length > 0) switchChat(chats[0].id);
    }
});