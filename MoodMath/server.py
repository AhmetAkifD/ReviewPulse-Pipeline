import pandas as pd
import joblib
import os
import shutil
from fastapi import FastAPI, File, UploadFile, Request
from fastapi.responses import HTMLResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
import io
import base64
from wordcloud import WordCloud

import data_cleaner
import stopword_remover
import model_trainer

# Ensure local directories exist
for folder in ["static/css", "static/js", "templates", "CSV", "Model"]:
    os.makedirs(folder, exist_ok=True)

app = FastAPI(title="MoodMath API")

app.mount("/static", StaticFiles(directory="static"), name="static")

@app.get("/", response_class=HTMLResponse)
async def serve_frontend():
    with open("templates/index.html", "r", encoding="utf-8") as f:
        return f.read()

def generate_wordcloud_base64(text, colormap):
    if not text.strip():
        return None
    wc = WordCloud(width=600, height=400, background_color="rgba(255,255,255,0)", mode="RGBA", colormap=colormap)
    wc.generate(text)
    img = wc.to_image()
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")

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
                return JSONResponse(status_code=400, content={"error": "JSON formatı hatası (Expected object or value). Lütfen dosya uzantınız (.json veya .csv) ile içeriğinin eşleştiğinden emin olun. CSV kullanıyorsanız dosya adınızın sonuna .csv ekleyin."})
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

        pos_text = " ".join(pos_df['NLP_Ready_Comment'].astype(str))
        neg_text = " ".join(neg_df['NLP_Ready_Comment'].astype(str))
        
        pos_wc = generate_wordcloud_base64(pos_text, "Greens")
        neg_wc = generate_wordcloud_base64(neg_text, "Reds")

        return {
            "message": "Temizlik ve NLP hazırlığı başarıyla tamamlandı!",
            "total": total_reviews,
            "pos": pos_count,
            "pos_wc": pos_wc,
            "neg": neg_count,
            "neg_wc": neg_wc
        }

    except Exception as e:
        return JSONResponse(status_code=400, content={"error": str(e)})

class TrainRequest(BaseModel):
    max_features: int = 5000
    test_size: float = 0.2

@app.post("/api/train")
async def train_model(req: TrainRequest):
    result = model_trainer.run_training(max_features=req.max_features, test_size=req.test_size)
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

    # Kullanıcının metnini aynen eğitirken yaptığımız gibi temizle ve köklerine ayır
    cleaned_input = data_cleaner.clean_text(req.text)
    processed_input = stopword_remover.process_text(cleaned_input)

    input_vector = vectorizer.transform([processed_input])
    probability = model.predict_proba(input_vector)[0].tolist()
    
    # 1. sınıf (Pozitif) için olasılığı çek ve yüzdeye çevir
    positivity_score = probability[1] * 100

    feature_names = vectorizer.get_feature_names_out()
    nonzero_indices = input_vector.nonzero()[1]
    words_found = [feature_names[i] for i in nonzero_indices]

    return {
        "positivity_score": positivity_score,
        "words_found": words_found
    }
