// Nav handles
const menuItems = document.querySelectorAll('.menu-item');
const sections = document.querySelectorAll('.section');

menuItems.forEach(item => {
    item.addEventListener('click', () => {
        menuItems.forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        
        sections.forEach(s => s.classList.remove('active'));
        document.getElementById(item.dataset.target).classList.add('active');
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