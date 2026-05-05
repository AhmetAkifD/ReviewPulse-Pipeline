import pandas as pd
import joblib
import os
import shutil
from fastapi import FastAPI, File, UploadFile, Request
from fastapi.responses import HTMLResponse, JSONResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
import io
import base64
import re
import json
import asyncio
import threading

import data_cleaner
import stopword_remover
import model_trainer
import google.generativeai as genai
import scraper

# CONFIG YÜKLEME
CONFIG_FILE = "config.json"
API_KEY_FILE = "API Keys/gemini.txt"

def load_config():
    # Önce varsayılanları belirle
    config_data = {
        "gemini_api_key": "",
        "scraper_wait_time": 10.5
    }
    
    # config.json oku
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE, "r", encoding="utf-8") as f:
            try:
                loaded = json.load(f)
                config_data.update(loaded)
            except:
                pass
    
    # gemini.txt oku (Leak önleme için buraya taşındı)
    if os.path.exists(API_KEY_FILE):
        with open(API_KEY_FILE, "r", encoding="utf-8") as f:
            config_data["gemini_api_key"] = f.read().strip()
            
    return config_data

def save_config(config_data):
    # API Key'i dosyaya yaz
    os.makedirs(os.path.dirname(API_KEY_FILE), exist_ok=True)
    with open(API_KEY_FILE, "w", encoding="utf-8") as f:
        f.write(config_data.get("gemini_api_key", ""))
    
    # Diğer ayarları config.json'a yaz (Key hariç)
    json_config = config_data.copy()
    if "gemini_api_key" in json_config:
        del json_config["gemini_api_key"]
        
    with open(CONFIG_FILE, "w", encoding="utf-8") as f:
        json.dump(json_config, f, indent=4)

config = load_config()

# GEMINI API AYARLARI
genai.configure(api_key=config["gemini_api_key"])
gemini_model = genai.GenerativeModel('gemini-2.5-flash')

# Ensure local directories exist
for folder in ["static/css", "static/js", "templates", "CSV", "Model", "Chats", "API Keys"]:
    os.makedirs(folder, exist_ok=True)


app = FastAPI(title="MoodMath API")

app.mount("/static", StaticFiles(directory="static"), name="static")


@app.get("/", response_class=HTMLResponse)
async def serve_frontend():
    with open("templates/index.html", "r", encoding="utf-8") as f:
        return f.read()


@app.post("/api/upload-clean")
async def upload_clean(file: UploadFile = File(...)):
    try:
        # Reset CSV directory to prevent overlapping runs if any
        if os.path.exists("CSV"):
            shutil.rmtree("CSV")
        os.makedirs("CSV")

        # Parse file
        try:
            contents = file.file.read()
            if file.filename.endswith(".json"):
                df = pd.read_json(io.BytesIO(contents))
            else:
                df = pd.read_csv(io.BytesIO(contents))
        except ValueError as ve:
            if "Expected object or value" in str(ve):
                return JSONResponse(status_code=400, content={
                    "error": "JSON formatı hatası (Expected object or value). Lütfen dosya uzantınız (.json veya .csv) ile içeriğinin eşleştiğinden emin olun. CSV kullanıyorsanız dosya adınızın sonuna .csv ekleyin."})
            return JSONResponse(status_code=400, content={"error": f"Veri okuma hatası: {str(ve)}"})

        # 1. Cleaner
        data_cleaner.run_cleaner(df)

        # 2. Stopword Remover
        stopword_remover.run_nltk_cleaning()

        # 3. Read metrics from NLP Ready
        final_df = pd.read_csv("CSV/nlp_ready_reviews.csv")
        total_reviews = len(final_df)

        pos_df = final_df[final_df['Sentiment'] == 1]
        neg_df = final_df[final_df['Sentiment'] == 0]

        pos_count = len(pos_df)
        neg_count = len(neg_df)

        return {
            "message": "Temizlik ve NLP hazırlığı başarıyla tamamlandı!",
            "total": total_reviews,
            "pos": pos_count,
            "neg": neg_count
        }

    except Exception as e:
        return JSONResponse(status_code=400, content={"error": str(e)})


class TrainRequest(BaseModel):
    test_size: float = 0.2


@app.post("/api/train")
async def train_model(req: TrainRequest):
    # Kelime dağarcığı burada 10000'e sabitlendi
    result = model_trainer.run_training(max_features=10000, test_size=req.test_size)
    if isinstance(result, tuple) and result[0] is not None:
        metrics, _ = result
        return {"message": "Eğitim başarılı!", "metrics": metrics}
    else:
        error_msg = result[1] if isinstance(result, tuple) else result
        return JSONResponse(status_code=400, content={"error": error_msg})


class PredictRequest(BaseModel):
    text: str


@app.post("/api/predict")
async def predict(req: PredictRequest):
    if not os.path.exists("Model/mood_model.pkl") or not os.path.exists("Model/mood_vectorizer.pkl"):
        return JSONResponse(status_code=400, content={"error": "Önce eğitim sayfasından model eğitmelisiniz!"})
    if len(req.text.strip()) < 3:
        return JSONResponse(status_code=400, content={"error": "Metin çok kısa!"})

    model = joblib.load("Model/mood_model.pkl")
    vectorizer = joblib.load("Model/mood_vectorizer.pkl")

    cleaned_input = data_cleaner.clean_text(req.text)
    processed_input = stopword_remover.process_text(cleaned_input)

    input_vector = vectorizer.transform([processed_input])
    probability = model.predict_proba(input_vector)[0].tolist()

    positivity_score = probability[1] * 100

    feature_names = vectorizer.get_feature_names_out()
    nonzero_indices = input_vector.nonzero()[1]
    words_found = [feature_names[i] for i in nonzero_indices]

    return {
        "positivity_score": positivity_score,
        "words_found": words_found
    }


class ArenaRequest(BaseModel):
    text: str


@app.post("/api/predict-arena")
async def predict_arena(req: ArenaRequest):
    if not os.path.exists("Model/mood_model.pkl") or not os.path.exists("Model/mood_vectorizer.pkl"):
        return JSONResponse(status_code=400, content={"error": "Önce eğitim sayfasından model eğitmelisiniz!"})
    if len(req.text.strip()) < 3:
        return JSONResponse(status_code=400, content={"error": "Metin çok kısa!"})

    # --- 1. KÖŞE: MOODMATH (Yerel Model) ---
    model = joblib.load("Model/mood_model.pkl")
    vectorizer = joblib.load("Model/mood_vectorizer.pkl")

    cleaned_input = data_cleaner.clean_text(req.text)
    processed_input = stopword_remover.process_text(cleaned_input)

    input_vector = vectorizer.transform([processed_input])
    probability = model.predict_proba(input_vector)[0].tolist()
    positivity_score = probability[1] * 100

    feature_names = vectorizer.get_feature_names_out()
    nonzero_indices = input_vector.nonzero()[1]
    words_found = [feature_names[i] for i in nonzero_indices]

    # --- 2. KÖŞE: GOOGLE GEMINI ---
    gemini_result = ""
    try:
        prompt = f"""
                Sen uzman bir e-ticaret duygu analizi botusun. Sana verilen müşteri yorumunu oku ve sadece şu formatta cevap ver:
                KARAR: [Sadece POZİTİF veya NEGATİF yaz]
                POZİTİFLİK: [%0 ile %100 arası bir sayı. Yorum tamamen negatifse 0'a yakın, tamamen pozitifse 100'e yakın bir oran yaz]
                SEBEP: [Kararını açıklayan tek bir kısa cümle yaz]

                Müşteri Yorumu: '{req.text}'
                """
        response = gemini_model.generate_content(prompt)
        gemini_result = response.text.strip()
    except Exception as e:
        gemini_result = f"Gemini API Hatası: {str(e)}"

    return {
        "moodmath": {
            "positivity_score": positivity_score,
            "words_found": words_found
        },
        "gemini": {
            "result": gemini_result
        }
    }


class ChatRequest(BaseModel):
    message: str
    chat_id: str

def get_chat_history(chat_id):
    filepath = f"Chats/{chat_id}.json"
    if os.path.exists(filepath):
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                return json.load(f)
        except:
            pass
    return []

def save_chat_history(chat_id, history):
    if len(history) > 10:
        history = history[-10:]
    filepath = f"Chats/{chat_id}.json"
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(history, f, indent=4, ensure_ascii=False)

@app.post("/api/chat")
async def chat_endpoint(req: ChatRequest):
    message = req.message.strip()
    chat_id = req.chat_id.strip()
    
    url_match = re.search(r"http[s]?://(?:[a-zA-Z]|[0-9]|[$-_@.&+]|[!*\\(\\),]|(?:%[0-9a-fA-F][0-9a-fA-F]))+", message)
    
    async def event_generator():
        queue = asyncio.Queue()
        loop = asyncio.get_event_loop()
        
        def progress_callback(msg):
            asyncio.run_coroutine_threadsafe(queue.put({"status": "progress", "message": msg}), loop)
            
        def worker():
            try:
                history = get_chat_history(chat_id)
                
                if url_match:
                    url = url_match.group(0)
                    user_instruction = message.replace(url, "").strip()
                    if not user_instruction:
                        user_instruction = "Bu ürünü yorumlara dayanarak analiz et."
                        
                    # Web Scraping
                    current_config = load_config()
                    scrape_result = scraper.scrape_reviews(url, progress_callback, wait_time=current_config["scraper_wait_time"])
                    product_name = scrape_result["product_name"]

                    reviews = scrape_result["reviews"]
                    
                    if not reviews:
                        progress_callback("Ürüne ait yorum bulunamadı veya çekilemedi.")
                        asyncio.run_coroutine_threadsafe(queue.put({
                            "status": "done", 
                            "reply": f"Maalesef <b>{product_name}</b> için yorum bulamadım."
                        }), loop)
                        return
                    
                    # Yerel Modeli Çalıştır (Temizlemesiz)
                    progress_callback("Yorumlar yerel MoodMath modelinde analiz ediliyor...")
                    model = joblib.load("Model/mood_model.pkl")
                    vectorizer = joblib.load("Model/mood_vectorizer.pkl")
                    
                    input_vector = vectorizer.transform(reviews)
                    probability = model.predict_proba(input_vector)
                    
                    avg_positivity = float(probability[:, 1].mean() * 100)
                    
                    feature_names = vectorizer.get_feature_names_out()
                    nonzero_indices = input_vector.nonzero()[1]
                    unique_indices = set(nonzero_indices)
                    words_found = [feature_names[i] for i in unique_indices]
                    words_str = ", ".join(list(words_found)[:50]) if words_found else "Belirgin kelime bulunamadı"
                    
                    # Yerel model metriklerini oku (varsa)
                    model_accuracy = "Bilinmiyor"
                    if os.path.exists("Model/metrics.json"):
                        try:
                            with open("Model/metrics.json", "r", encoding="utf-8") as f:
                                m_data = json.load(f)
                                model_accuracy = f"%{m_data['accuracy']*100:.1f}"
                        except:
                            pass

                    # Gemini İstediği
                    progress_callback("Analiz sonuçları uzman Gemini'ye gönderiliyor...")
                    
                    prompt = f"""Sen ürün yorumlarını inceleyen ve ürün hakkındaki düşüncüleri analiz eden bir uzmansın. Vereceğim verileri inceleyip birkaç cümlelik, profesyonel bir geri dönüş yap.
Analizi yaparken MoodMath yerel modelinin sonuçlarını baz al. Bu modelin genel başarı oranı (Accuracy): {model_accuracy}.

Kullanıcı senden şunu istiyor: {message}

İhtiyacın olan veriler:
Duygu Yoğunluğu: %{avg_positivity:.1f} Pozitif
Dikkat Edilen Kelimeler: {words_str}"""

                    response = gemini_model.generate_content(prompt)
                    gemini_result = response.text.strip()
                    
                    gemini_html = gemini_result.replace('\n', '<br>')
                    
                    final_reply = f"""
                    <strong style="color: var(--primary);">{product_name}</strong><br>
                    <strong style="color: {'var(--success)' if avg_positivity >= 50 else 'var(--error)'};">MoodMath Duygu Skoru: %{avg_positivity:.1f} Pozitif</strong> 
                    <span style="font-size: 0.8rem; color: var(--text-muted);">(Model Başarımı: {model_accuracy})</span><br><br>
                    {gemini_html}
                    """
                    
                    history.append({"role": "user", "content": message})
                    history.append({"role": "bot", "content": final_reply.strip()})
                    save_chat_history(chat_id, history)
                    
                    asyncio.run_coroutine_threadsafe(queue.put({"status": "done", "reply": final_reply}), loop)
                    
                else:
                    progress_callback("Mesajınız uzman tarafından değerlendiriliyor...")
                    
                    history_text = ""
                    for h in history:
                        role_str = "Kullanıcı" if h["role"] == "user" else "Sen (Uzman)"
                        history_text += f"{role_str}: {h['content']}\n"
                        
                    prompt = f"""Sen bir ürün inceleme uzmanısın. Kullanıcının senden istediği şu: {message}
                    
Kullanıcı ile önceden şunları konuştun:
{history_text}

Kullanıcıya profesyonel bir dönüş yap. Dönüşlerini kısa tut. Yanıtlarında asla markdown formatı (**, *, # gibi işaretler) kullanma, sadece düz metin kullan."""

                    response = gemini_model.generate_content(prompt)
                    gemini_result = response.text.strip()
                    gemini_html = gemini_result.replace('\n', '<br>')
                    
                    history.append({"role": "user", "content": message})
                    history.append({"role": "bot", "content": gemini_html.strip()})
                    save_chat_history(chat_id, history)
                    
                    asyncio.run_coroutine_threadsafe(queue.put({"status": "done", "reply": gemini_html}), loop)
                    
            except Exception as e:
                asyncio.run_coroutine_threadsafe(queue.put({"status": "error", "message": str(e)}), loop)

        thread = threading.Thread(target=worker)
        thread.start()
        
        while True:
            msg = await queue.get()
            yield json.dumps(msg) + "\n"
            if msg["status"] in ["done", "error"]:
                break
                
    return StreamingResponse(event_generator(), media_type="application/x-ndjson")


class SettingsRequest(BaseModel):
    gemini_api_key: str
    scraper_wait_time: float

@app.get("/api/settings")
async def get_settings():
    return load_config()

@app.post("/api/settings")
async def update_settings(req: SettingsRequest):
    new_config = {
        "gemini_api_key": req.gemini_api_key,
        "scraper_wait_time": req.scraper_wait_time
    }
    save_config(new_config)
    
    # Gemini'yi yeniden yapılandır
    genai.configure(api_key=new_config["gemini_api_key"])
    
    return {"message": "Ayarlar başarıyla kaydedildi!"}

@app.get("/api/model-metrics")
async def get_model_metrics():
    if os.path.exists("Model/metrics.json"):
        with open("Model/metrics.json", "r", encoding="utf-8") as f:
            return json.load(f)
    return JSONResponse(status_code=404, content={"error": "Metrik dosyası bulunamadı. Lütfen önce modeli eğitin."})


