import pandas as pd
import nltk
from nltk.corpus import stopwords
from TurkishStemmer import TurkishStemmer  # YENİ: NLTK yerine saf Türkçe kök bulucu!


def run_nltk_cleaning():
    print("\n--- 2. AŞAMA: Stop-words Ayıklaması ve Kök Bulma Başlıyor ---")

    file_path = "CSV/cleaned_reviews.csv"
    try:
        df = pd.read_csv(file_path, encoding='utf-8')
    except FileNotFoundError:
        print(f"Hata: Önce 1. aşamayı çalıştırıp '{file_path}' dosyasını oluşturmalısın!")
        return

    # Kütüphaneleri ve kelimeleri hazırla
    nltk.download('stopwords', quiet=True)
    stop_words = set(stopwords.words('turkish'))
    custom_stopwords = {'bir', 'bu', 'şu', 'o', 'ürün', 'aldım', 'geldi', 'çok', 'için', 'diye', 've', 'ile', 'da',
                        'de', 'daha', 'en'}
    stop_words.update(custom_stopwords)

    # YENİ: Türkçe kök bulucuyu başlat
    stemmer = TurkishStemmer()

    def process_text(text):
        if type(text) != str: return ""

        words = text.split()
        processed_words = []

        for word in words:
            # Önce kelime etkisiz mi (stop-word) diye bak
            if word not in stop_words:
                # Etkisiz değilse, kelimenin KÖKÜNÜ bul
                root_word = stemmer.stem(word)
                processed_words.append(root_word)

        return " ".join(processed_words)

    # Fonksiyonu tüm yorumlara uygula
    df['NLP_Ready_Comment'] = df['Cleaned_Comment'].apply(process_text)

    # Boş kalan satırları temizle
    df = df[df['NLP_Ready_Comment'] != ""]
    df = df.dropna(subset=['NLP_Ready_Comment'])

    final_file_path = "CSV/nlp_ready_reviews.csv"
    df.to_csv(final_file_path, index=False, encoding='utf-8')
    print(f"2. Aşama Tamamlandı! Köklerine ayrılmış NLP verisi '{final_file_path}' olarak kaydedildi.\n")