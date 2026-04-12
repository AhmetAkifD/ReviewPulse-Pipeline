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
        if file.filename.endswith(".json"):
            df = pd.read_json(file.file)
        else:
            df = pd.read_csv(file.file)

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

@app.post("/api/batch-analyze")
async def batch_analyze(file: UploadFile = File(...)):
    if not os.path.exists("Model/mood_model.pkl"):
        return JSONResponse(status_code=400, content={"error": "Önce model eğitmelisiniz!"})
    try:
        if file.filename.endswith(".json"):
            df_new = pd.read_json(file.file)
        else:
            df_new = pd.read_csv(file.file)

        model = joblib.load("Model/mood_model.pkl")
        vectorizer = joblib.load("Model/mood_vectorizer.pkl")

        df_new['Cleaned'] = df_new['Comment'].apply(lambda x: str(x).lower())
        X_new = vectorizer.transform(df_new['Cleaned'])
        df_new['Tahmin'] = model.predict(X_new)
        df_new['Duygu'] = df_new['Tahmin'].map({1: "Pozitif 😊", 0: "Negatif 😡"})
        
        # Prepare for CSV download return (we'll return JSON and front-end handles download)
        csv_data = df_new.to_csv(index=False)
        preview = df_new[['Comment', 'Duygu']].head(10).to_dict(orient="records")

        return {
            "message": "Analiz başarılı",
            "total": len(df_new),
            "preview": preview,
            "csv_string": csv_data
        }
    except Exception as e:
        return JSONResponse(status_code=400, content={"error": f"Analiz hatası: {str(e)}"})

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

    input_vector = vectorizer.transform([req.text.lower()])
    prediction = int(model.predict(input_vector)[0])
    probability = model.predict_proba(input_vector)[0].tolist()

    feature_names = vectorizer.get_feature_names_out()
    nonzero_indices = input_vector.nonzero()[1]
    words_found = [feature_names[i] for i in nonzero_indices]

    return {
        "prediction": prediction,
        "confidence": probability[prediction] * 100,
        "words_found": words_found
    }
