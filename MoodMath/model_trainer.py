import pandas as pd
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.model_selection import train_test_split
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score
import joblib
import os


def run_training(max_features=5000, test_size=0.2):
    file_path = "CSV/nlp_ready_reviews.csv"
    try:
        df = pd.read_csv(file_path, encoding='utf-8')
    except FileNotFoundError:
        return None, "Hata: Önce 2. aşamayı çalıştırıp veriyi hazırlamalısın!"

    df = df.dropna(subset=['NLP_Ready_Comment'])

    X = df['NLP_Ready_Comment']
    Y = df['Sentiment']

    # 1. Slider'dan gelen 'max_features' değerine göre vektörize ediyoruz
    # YENİ: Modelin sadece gerçekten ayırt edici ve en az 3 farklı yorumda geçen kelimeleri öğrenmesini sağlıyoruz
    vectorizer = TfidfVectorizer(
        max_features=max_features,
        ngram_range=(1, 2),
        min_df=3,  # Alt Limit: En az 3 farklı yorumda geçmeyen kelimeleri (yazım hataları, özel isimler) sil
        max_df=0.85  # Üst Limit: Yorumların %85'inde geçen (aşırı yaygın) kelimeleri sil
    )
    X_vectorized = vectorizer.fit_transform(X)

    # 2. Slider'dan gelen 'test_size' değerine göre bölüyoruz
    X_train, X_test, Y_train, Y_test = train_test_split(X_vectorized, Y, test_size=test_size, random_state=42)

    # 3. Model Eğitimi
    model = LogisticRegression(class_weight='balanced')
    model.fit(X_train, Y_train)

    # 4. Sınav
    Y_pred = model.predict(X_test)
    accuracy = accuracy_score(Y_test, Y_pred)

    # 5. MODELİ KAYDETME (Web arayüzü buradan okuyacak)
    if not os.path.exists("Model"):
        os.makedirs("Model")

    joblib.dump(model, "Model/mood_model.pkl")
    joblib.dump(vectorizer, "Model/mood_vectorizer.pkl")

    # Sonucu ve analiz için dataframe'i geri döndür
    return accuracy, df