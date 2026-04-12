import pandas as pd
import re
import os

# Artık fonksiyonumuz dışarıdan bir 'df' (Dataframe) kabul ediyor!
def run_cleaner(df):
    print("\n--- 1. AŞAMA: Ham Veri Temizliği Başlıyor ---")

    def clean_text(text):
        if type(text) != str: return ""
        text = text.replace('İ', 'i').replace('I', 'ı')
        text = text.lower()
        text = re.sub(r'[^a-zçğıöşü\s]', ' ', text)
        text = re.sub(r'\s+', ' ', text).strip()
        return text

    df['Cleaned_Comment'] = df['Comment'].apply(clean_text)
    df = df[df['Cleaned_Comment'] != ""]
    df = df.dropna(subset=['Cleaned_Comment'])

    def map_sentiment(rating):
        return 0 if rating <= 3 else 1

    df['Sentiment'] = df['Rating'].apply(map_sentiment)

    # Eğer CSV klasörü yanlışlıkla silindiyse kod çökmesin diye güvenlik önlemi
    if not os.path.exists("CSV"):
        os.makedirs("CSV")

    cleaned_file_path = "CSV/cleaned_reviews.csv"
    df.to_csv(cleaned_file_path, index=False, encoding='utf-8')
    print(f"1. Aşama Tamamlandı! Veri '{cleaned_file_path}' olarak kaydedildi.\n")