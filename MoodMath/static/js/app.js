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
const sliderF = document.getElementById('sliderFeat');
const featV = document.getElementById('featVal');
sliderF.addEventListener('input', () => featV.innerText = sliderF.value);

const sliderT = document.getElementById('sliderTest');
const testV = document.getElementById('testVal');
sliderT.addEventListener('input', () => testV.innerText = sliderT.value);

document.getElementById('btnTrain').addEventListener('click', async () => {
    setLoading('btnTrain', true);
    hideAlert('alertP2');
    try {
        const payload = {
            max_features: parseInt(sliderF.value),
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

// Page 3: Batch
const dropArea2 = document.getElementById('dropArea2');
const uploadBatch = document.getElementById('uploadFileBatch');
const fileInfo2 = document.getElementById('fileInfo2');
let currentCSV = null;

dropArea2.addEventListener('click', () => uploadBatch.click());
uploadBatch.addEventListener('change', () => {
    if(uploadBatch.files.length > 0) fileInfo2.innerText = "Seçilen dosya: " + uploadBatch.files[0].name;
});

document.getElementById('btnBatch').addEventListener('click', async () => {
    if(!uploadBatch.files.length) return showAlert('alertP3', 'error', 'Lütfen bir veri dosyası seçin!');
    setLoading('btnBatch', true);
    hideAlert('alertP3');
    const formData = new FormData();
    formData.append("file", uploadBatch.files[0]);

    try {
        const r = await fetch('/api/batch-analyze', { method: 'POST', body: formData });
        const data = await r.json();
        
        if(!r.ok) throw new Error(data.error);
        
        showAlert('alertP3', 'success', `${data.total} Kayıt analiz edildi!`);
        document.getElementById('dashboard3').style.display = 'block';
        
        currentCSV = data.csv_string;
        
        const tbody = document.getElementById('batchTable');
        tbody.innerHTML = "";
        data.preview.forEach(row => {
            const tr = document.createElement('tr');
            tr.innerHTML = `<td>${row.Comment}</td><td><span class="badge ${row.Duygu.includes('Pozitif')?'pos':'neg'}">${row.Duygu}</span></td>`;
            tbody.appendChild(tr);
        });
    } catch(err) {
        showAlert('alertP3', 'error', err.message);
    } finally {
        setLoading('btnBatch', false);
    }
});

document.getElementById('btnDownload').addEventListener('click', () => {
    if(!currentCSV) return;
    const blob = new Blob([currentCSV], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'analiz_sonuclari.csv';
    a.click();
    URL.revokeObjectURL(url);
});

// Page 4: Predict
document.getElementById('btnPredict').addEventListener('click', async () => {
    const text = document.getElementById('txtPredict').value;
    if(text.length < 3) return showAlert('alertP4', 'error', 'Çok kısa metin!');
    
    setLoading('btnPredict', true);
    hideAlert('alertP4');
    try {
        const r = await fetch('/api/predict', {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({ text })
        });
        const data = await r.json();
        if(!r.ok) throw new Error(data.error);

        document.getElementById('dashboard4').style.display = 'block';
        
        const predBox = document.getElementById('predBox');
        predBox.className = data.prediction === 1 ? 'metric-box success' : 'metric-box error';
        
        document.getElementById('predVal').innerText = data.prediction === 1 ? 'Pozitif 😊' : 'Negatif 😡';
        document.getElementById('predConf').innerText = `Eminlik Oranı: %${data.confidence.toFixed(1)}`;

        const wordsDiv = document.getElementById('predWords');
        wordsDiv.innerHTML = "";
        if(data.words_found.length === 0) {
            wordsDiv.innerHTML = `<div class="alert visible" style="background: rgba(255,255,255,0.05); color:#94a3b8; border:1px solid rgba(255,255,255,0.1)">🤔 Model eşleşen bir kelime bulamadı, rastgele/ağırlık bazlı tahmin yapıldı.</div>`;
        } else {
            data.words_found.forEach(w => {
                const sp = document.createElement('span');
                sp.className = 'badge pos';
                sp.style.background = 'linear-gradient(135deg, rgba(59, 130, 246, 0.4), rgba(37, 99, 235, 0.4))';
                sp.style.color = '#bfdbfe';
                sp.style.border = '1px solid rgba(59, 130, 246, 0.5)';
                sp.innerText = w;
                wordsDiv.appendChild(sp);
            });
        }
    } catch(err) {
        showAlert('alertP4', 'error', err.message);
    } finally {
        setLoading('btnPredict', false);
    }
});
