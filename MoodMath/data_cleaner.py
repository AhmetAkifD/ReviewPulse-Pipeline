import pandas as pd
import re
import os

# Emoji Sözlüğü
emoji_dict = {
    "😊": " mutlu ", "😀": " mutlu ", "😃": " mutlu ", "😁": " mutlu ", "😆": " komik ", "😂": " komik ",
    "🤣": " komik ", "🥰": " sevgi ", "😍": " harika ", "🤩": " harika ", "😘": " öpücük ", "😗": " öpücük ",
    "🤍": " kalp ", "❤️": " kalp ", "💕": " kalp ", "💖": " kalp ", "💗": " kalp ", "💙": " kalp ",
    "👍": " onay ", "👌": " tamam ", "👏": " tebrik ", "🙌": " kutlama ", "🤝": " anlaşma ",
    "🔥": " harika ", "✨": " parıltı ", "🌟": " yıldız ", "💯": " mükemmel ", 
    "😔": " üzgün ", "😞": " üzgün ", "😟": " endişeli ", "😠": " kızgın ", "😡": " kızgın ", "🤬": " küfür ",
    "😭": " ağlama ", "😢": " ağlama ", "💔": " kırık_kalp ", "👎": " ret ", "🤦": " hayal_kırıklığı ",
    "🗑️": " çöp ", "💩": " kötü ", "🤮": " iğrenç ", "🤢": " iğrenç "
}

def clean_text(text):
    if type(text) != str: return ""
    # Emojileri metne çevir
    for emoji_char, word_meaning in emoji_dict.items():
        text = text.replace(emoji_char, word_meaning)
        
    text = text.replace('İ', 'i').replace('I', 'ı')
    text = text.lower()
    text = re.sub(r'[^a-zçğıöşü\s]', ' ', text)
    text = re.sub(r'\s+', ' ', text).strip()
    return text

def map_sentiment(rating):
    return 0 if rating <= 3 else 1

# Artık fonksiyonumuz dışarıdan bir 'df' (Dataframe) kabul ediyor!
def run_cleaner(df):
    df['Cleaned_Comment'] = df['Comment'].apply(clean_text)
    df = df[df['Cleaned_Comment'] != ""]
    df = df.dropna(subset=['Cleaned_Comment'])

    df['Sentiment'] = df['Rating'].apply(map_sentiment)

    # Eğer CSV klasörü yanlışlıkla silindiyse kod çökmesin diye güvenlik önlemi
    if not os.path.exists("CSV"):
        os.makedirs("CSV")

    cleaned_file_path = "CSV/cleaned_reviews.csv"
    df.to_csv(cleaned_file_path, index=False, encoding='utf-8')