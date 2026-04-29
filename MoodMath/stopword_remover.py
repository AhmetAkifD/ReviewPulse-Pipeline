import pandas as pd
import nltk
from nltk.corpus import stopwords
from TurkishStemmer import TurkishStemmer  # YENİ: NLTK yerine saf Türkçe kök bulucu!


# Global initialization so it can be used for single predictions
nltk.download('stopwords', quiet=True)
stop_words = set(stopwords.words('turkish'))
custom_stopwords = {'bir', 'bu', 'şu', 'o', 'ürün', 'aldım', 'geldi', 'çok', 'için', 'diye', 've', 'ile', 'da',
                    'de', 'daha', 'en', 'şey', 'ne', 'kadar', 'var', 'yok', 'ama', 'fakat', 'gibi'}
stop_words.update(custom_stopwords)
stemmer = TurkishStemmer()

def process_text(text):
    if type(text) != str: return ""

    words = text.split()
    processed_words = []

    for word in words:
        root_word = stemmer.stem(word)
        # Önce kelime etkisiz mi (stop-word) diye bak, ayrıca kökü de kontrol et! (Örn: 'biri' -> 'bir')
        if word not in stop_words and root_word not in stop_words:
            processed_words.append(root_word)

    return " ".join(processed_words)

def run_nltk_cleaning():
    file_path = "CSV/cleaned_reviews.csv"
    try:
        df = pd.read_csv(file_path, encoding='utf-8')
    except FileNotFoundError:
        raise FileNotFoundError(f"Hata: '{file_path}' dosyası bulunamadı. Lütfen önce 1. aşamayı tamamlayın.")

    # Fonksiyonu tüm yorumlara uygula
    df['NLP_Ready_Comment'] = df['Cleaned_Comment'].apply(process_text)

    # Boş kalan satırları temizle
    df = df[df['NLP_Ready_Comment'] != ""]
    df = df.dropna(subset=['NLP_Ready_Comment'])

    final_file_path = "CSV/nlp_ready_reviews.csv"
    df.to_csv(final_file_path, index=False, encoding='utf-8')