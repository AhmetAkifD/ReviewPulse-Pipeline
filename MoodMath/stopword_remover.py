import pandas as pd
import nltk
from nltk.corpus import stopwords


def run_nltk_cleaning():
    print("\n--- 2. AŞAMA: Etkisiz Kelime (Stop-words) Ayıklaması Başlıyor ---")

    file_path = "CSV/cleaned_reviews.csv"
    try:
        df = pd.read_csv(file_path, encoding='utf-8')
    except FileNotFoundError:
        print(f"Hata: Önce 1. aşamayı çalıştırıp '{file_path}' dosyasını oluşturmalısın!")
        return

    nltk.download('stopwords', quiet=True)  # quiet=True terminali gereksiz yazılarla doldurmaz
    stop_words = set(stopwords.words('turkish'))
    custom_stopwords = {'bir', 'bu', 'şu', 'o', 'ürün', 'aldım', 'geldi', 'çok', 'için', 'diye'}
    stop_words.update(custom_stopwords)

    def remove_stopwords(text):
        if type(text) != str: return ""
        words = text.split()
        filtered_words = [word for word in words if word not in stop_words]
        return " ".join(filtered_words)

    df['NLP_Ready_Comment'] = df['Cleaned_Comment'].apply(remove_stopwords)
    df = df[df['NLP_Ready_Comment'] != ""]
    df = df.dropna(subset=['NLP_Ready_Comment'])

    final_file_path = "CSV/nlp_ready_reviews.csv"
    df.to_csv(final_file_path, index=False, encoding='utf-8')
    print(f"2. Aşama Tamamlandı! NLP'ye hazır veri '{final_file_path}' olarak kaydedildi.\n")